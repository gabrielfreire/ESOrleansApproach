using ESOrleansApproach.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ESOrleansApproach.Infrastructure.Persistence
{
    public static class DbContextUtils
    {
        private sealed class CachedPropertyInfo
        {
            public string Name;
            public Type PropertyType;
            public bool CanWrite;
            public Func<object, object> GetValue;
            public Action<object, object> SetValue;
            public PropertyCategory Category;
            public Type CollectionElementType;
        }

        private enum PropertyCategory
        {
            Scalar,
            Collection,
            NavigationEntity,
            Skip
        }

        private static readonly ConcurrentDictionary<Type, CachedPropertyInfo[]> PropertyCache = new();
        private static readonly ConcurrentDictionary<Type, Func<object, object>> IdGetterCache = new();

        public static List<DomainEventChange> TrackChanges<T>(T target, T source, DbContext dbContext, ref List<DomainEventChange> domainEventChanges)
            where T : class
        {
            if (target is null || source is null)
                return domainEventChanges;

            if (target is not StateBase || source is not StateBase)
                return domainEventChanges;

            var entry = dbContext.Entry(target);
            var entityId = ((StateBase)(object)target).Id;
            var entityType = target.GetType();
            var changes = 0;

            foreach (var prop in GetCachedProperties(entityType))
            {
                switch (prop.Category)
                {
                    case PropertyCategory.Scalar:
                        changes += TrackScalarProperty(prop, target, source, entry, entityId, entityType.FullName!, ref domainEventChanges);
                        break;

                    case PropertyCategory.Collection:
                        changes += TrackCollectionProperty(prop, target, source, dbContext, entityId, entityType.FullName!, ref domainEventChanges);
                        break;

                    case PropertyCategory.NavigationEntity:
                        changes += TrackNavigationProperty(prop, target, source, dbContext, entityId, entityType.FullName!, ref domainEventChanges);
                        break;
                }
            }

            if (changes == 0)
            {
                entry.State = EntityState.Unchanged;
            }
            else if (entry.State != EntityState.Added && entry.State != EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
            }

            return domainEventChanges;
        }

        private static int TrackScalarProperty<T>(CachedPropertyInfo prop, T target, T source, EntityEntry<T> entry, Guid entityId, string entityName, ref List<DomainEventChange> domainEventChanges)
            where T : class
        {
            if (!prop.CanWrite || prop.SetValue is null)
                return 0;

            if (prop.Name == nameof(StateBase.UpdatedOnUtc))
                return 0;

            var sourceValue = prop.GetValue(source!);
            var targetValue = prop.GetValue(target!);

            if (Equals(sourceValue, targetValue))
                return 0;

            prop.SetValue(target!, sourceValue);
            if (prop.Name != nameof(StateBase.Id))
                entry.Property(prop.Name).IsModified = true;

            domainEventChanges.Add(new DomainEventChange
            {
                EntityType = prop.PropertyType.FullName ?? prop.PropertyType.Name,
                EntityId = entityId,
                FieldName = prop.Name,
                PreviousValue = targetValue,
                CurrentValue = sourceValue,
                Action = "Update",
                Timestamp = DateTimeOffset.UtcNow,
                Entity = entityName
            });

            return 1;
        }

        private static int TrackCollectionProperty<T>(CachedPropertyInfo prop, T target, T source, DbContext dbContext, Guid entityId, string entityName, ref List<DomainEventChange> domainEventChanges)
            where T : class
        {
            var targetCollection = prop.GetValue(target!) as IList;
            var sourceCollection = prop.GetValue(source!) as IList;
            if (targetCollection is null || sourceCollection is null)
                return 0;

            var elementType = prop.CollectionElementType ?? typeof(object);
            if (typeof(StateBase).IsAssignableFrom(elementType))
            {
                return TrackEntityCollection(prop, targetCollection, sourceCollection, dbContext, entityId, entityName, ref domainEventChanges);
            }

            if (CollectionsEqual(targetCollection, sourceCollection))
                return 0;

            if (!prop.CanWrite || prop.SetValue is null)
                return 0;

            prop.SetValue(target!, sourceCollection);
            domainEventChanges.Add(new DomainEventChange
            {
                EntityType = prop.PropertyType.FullName ?? prop.PropertyType.Name,
                EntityId = entityId,
                FieldName = prop.Name,
                Action = "Update",
                Timestamp = DateTimeOffset.UtcNow,
                Entity = entityName
            });

            return 1;
        }

        private static int TrackEntityCollection(CachedPropertyInfo prop, IList targetCollection, IList sourceCollection, DbContext dbContext, Guid entityId, string entityName, ref List<DomainEventChange> domainEventChanges)
        {
            var changes = 0;
            var sourceById = new Dictionary<object, object>();

            foreach (var sourceItem in sourceCollection)
            {
                var id = GetIdValue(sourceItem);
                if (id is not null)
                    sourceById[id] = sourceItem!;
            }

            for (var i = targetCollection.Count - 1; i >= 0; i--)
            {
                var targetItem = targetCollection[i];
                var targetId = GetIdValue(targetItem);
                if (targetId is not null && !sourceById.ContainsKey(targetId))
                {
                    dbContext.Entry(targetItem!).State = EntityState.Deleted;
                    targetCollection.RemoveAt(i);
                    changes++;
                    domainEventChanges.Add(new DomainEventChange
                    {
                        EntityType = prop.PropertyType.FullName ?? prop.PropertyType.Name,
                        EntityId = entityId,
                        FieldName = prop.Name,
                        Action = "Delete",
                        Timestamp = DateTimeOffset.UtcNow,
                        Entity = entityName
                    });
                }
            }

            var targetById = new Dictionary<object, object>();
            foreach (var targetItem in targetCollection)
            {
                var id = GetIdValue(targetItem);
                if (id is not null)
                    targetById[id] = targetItem!;
            }

            foreach (var sourceItem in sourceCollection)
            {
                var sourceId = GetIdValue(sourceItem);
                if (sourceId is null)
                    continue;

                if (!targetById.TryGetValue(sourceId, out var targetItem))
                {
                    targetCollection.Add(sourceItem!);
                    dbContext.Entry(sourceItem!).State = EntityState.Added;
                    changes++;
                    domainEventChanges.Add(new DomainEventChange
                    {
                        EntityType = prop.PropertyType.FullName ?? prop.PropertyType.Name,
                        EntityId = entityId,
                        FieldName = prop.Name,
                        Action = "Add",
                        Timestamp = DateTimeOffset.UtcNow,
                        Entity = entityName
                    });
                    continue;
                }

                try
                {
                    TrackChanges(targetItem, sourceItem!, dbContext, ref domainEventChanges);
                }
                catch (Exception ex)
                {
                    LogUtils.LogError(nameof(DbContextUtils), ex.Message, nameof(DbContextUtils), ex.ToString());
                }
            }

            return changes;
        }

        private static int TrackNavigationProperty<T>(CachedPropertyInfo prop, T target, T source, DbContext dbContext, Guid entityId, string entityName, ref List<DomainEventChange> domainEventChanges)
            where T : class
        {
            var sourceValue = prop.GetValue(source!);
            var targetValue = prop.GetValue(target!);

            if (sourceValue is null && targetValue is null)
                return 0;

            if (sourceValue is null && targetValue is not null)
            {
                if (prop.SetValue is not null)
                    prop.SetValue(target!, null);

                dbContext.Entry(targetValue).State = EntityState.Deleted;
                domainEventChanges.Add(new DomainEventChange
                {
                    EntityType = prop.PropertyType.FullName ?? prop.PropertyType.Name,
                    EntityId = entityId,
                    FieldName = prop.Name,
                    Action = "Delete",
                    Timestamp = DateTimeOffset.UtcNow,
                    Entity = entityName
                });

                return 1;
            }

            if (sourceValue is not null && targetValue is null)
            {
                if (prop.SetValue is null)
                    return 0;

                prop.SetValue(target!, sourceValue);
                dbContext.Entry(sourceValue).State = EntityState.Added;
                domainEventChanges.Add(new DomainEventChange
                {
                    EntityType = prop.PropertyType.FullName ?? prop.PropertyType.Name,
                    EntityId = entityId,
                    FieldName = prop.Name,
                    Action = "Create",
                    Timestamp = DateTimeOffset.UtcNow,
                    Entity = entityName
                });

                return 1;
            }

            try
            {
                TrackChanges(targetValue!, sourceValue!, dbContext, ref domainEventChanges);
            }
            catch (Exception ex)
            {
                LogUtils.LogError(nameof(DbContextUtils), ex.Message, nameof(DbContextUtils), ex.ToString());
            }

            return 0;
        }

        private static CachedPropertyInfo[] GetCachedProperties(Type type)
        {
            return PropertyCache.GetOrAdd(type, static ownerType =>
            {
                var props = ownerType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var result = new List<CachedPropertyInfo>(props.Length);

                foreach (var prop in props)
                {
                    if (prop.GetMethod is null)
                        continue;

                    if (prop.Name == nameof(StateBase.DeletedEntities))
                        continue;

                    var propertyType = prop.PropertyType;
                    var category = ResolveCategory(propertyType);
                    result.Add(new CachedPropertyInfo
                    {
                        Name = prop.Name,
                        PropertyType = propertyType,
                        CanWrite = prop.SetMethod is not null,
                        GetValue = BuildGetter(ownerType, prop),
                        SetValue = prop.SetMethod is not null ? BuildSetter(ownerType, prop) : null,
                        Category = category,
                        CollectionElementType = category == PropertyCategory.Collection && propertyType.IsGenericType
                            ? propertyType.GetGenericArguments()[0]
                            : null
                    });
                }

                return result.ToArray();
            });
        }

        private static PropertyCategory ResolveCategory(Type propertyType)
        {
            if (propertyType == typeof(string) || propertyType == typeof(Guid) || propertyType.IsEnum || propertyType.IsValueType)
                return PropertyCategory.Scalar;

            if (typeof(IEnumerable).IsAssignableFrom(propertyType) && propertyType != typeof(string))
                return PropertyCategory.Collection;

            if (typeof(StateBase).IsAssignableFrom(propertyType))
                return PropertyCategory.NavigationEntity;

            return PropertyCategory.Skip;
        }

        private static Func<object, object> BuildGetter(Type ownerType, PropertyInfo prop)
        {
            var param = Expression.Parameter(typeof(object), "obj");
            var body = Expression.Convert(Expression.Property(Expression.Convert(param, ownerType), prop), typeof(object));
            return Expression.Lambda<Func<object, object>>(body, param).Compile();
        }

        private static Action<object, object> BuildSetter(Type ownerType, PropertyInfo prop)
        {
            var objParam = Expression.Parameter(typeof(object), "obj");
            var valParam = Expression.Parameter(typeof(object), "value");
            var body = Expression.Assign(
                Expression.Property(Expression.Convert(objParam, ownerType), prop),
                Expression.Convert(valParam, prop.PropertyType));
            return Expression.Lambda<Action<object, object>>(body, objParam, valParam).Compile();
        }

        private static object GetIdValue(object value)
        {
            if (value is null)
                return null;

            var getter = IdGetterCache.GetOrAdd(value.GetType(), static type =>
            {
                var idProperty = type.GetProperty(nameof(StateBase.Id));
                return idProperty is null ? null : BuildGetter(type, idProperty);
            });

            return getter?.Invoke(value);
        }

        private static bool CollectionsEqual(IList left, IList right)
        {
            if (left.Count != right.Count)
                return false;

            for (var i = 0; i < left.Count; i++)
            {
                if (!Equals(left[i], right[i]))
                    return false;
            }

            return true;
        }
    }
}