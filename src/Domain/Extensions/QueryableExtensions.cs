using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Extensions
{
    public static class QueryableExtensions
    {
        public static IQueryable<T> GetQueryable<T>(
            this IQueryable<T> queryable,
            ICollection<Tuple<string, object>>? query = null) where T : StateBase
        {

            if (query is null)
                query = new List<Tuple<string, object>>();

            // make sure we only returned non-deleted records by default
            if (!query.Any(t => t.Item1.ToLower() == "deleted"))
            {
                queryable = queryable.Where(p => !p.Deleted);
            }

            var tenantQuery = query.FirstOrDefault(t => t.Item1.ToLower() == nameof(StateBase.Tenant).ToLower());

            if (tenantQuery is not null)
            {
                if (tenantQuery.Item2 == (object)"*")
                    query.Remove(tenantQuery);
            }

            var dateFiltered = false;
            foreach (var q in query)
            {
                var _param = q.Item1.ToLower();
                var _value = q.Item2;

                switch (_param)
                {
                    case "ids":
                        var ids = (List<Guid>)_value;

                        if (ids.Any())
                            queryable = queryable.Where(p => ids.Contains(p.Id));
                        break;
                    
                    case "sortfield":
                        var _expression = GetLambdaExpression<T>(((string)_value));
                        var _sortDirection = "desc";

                        var _sortDirectionQuery = query.FirstOrDefault(q => q.Item1 == "sortdirection");
                        if (_sortDirectionQuery != null)
                            _sortDirection = ((string)_sortDirectionQuery.Item2).ToLower();

                        if (_sortDirection == "asc")
                        {
                            queryable = queryable.OrderBy(_expression);
                        }
                        else
                        {
                            queryable = queryable.OrderByDescending(_expression);
                        }
                        break;
                    case "skip":
                        queryable = queryable.Skip(int.Parse(_value.ToString()));
                        break;
                    case "count":
                        queryable = queryable.Take(int.Parse(_value.ToString()));
                        break;
                    default:
                        if (typeof(T).GetProperties().Any(p => p.Name.ToLower() == _param))
                        {
                            queryable = queryable.ApplyFilterForPrimitiveTypes(_param, _value);
                        }
                        break;
                }

            }

            return queryable;
        }
        public static Expression<Func<T, object>> GetLambdaExpression<T>(string propertyName)
        {
            var _rootExpression = Expression.Parameter(typeof(T), nameof(T));
            var _conversion = Expression.Convert(Expression.Property(_rootExpression, propertyName), typeof(object));
            return Expression.Lambda<Func<T, object>>(_conversion, _rootExpression);
        }
        public static IQueryable<T> ApplyFilterForPrimitiveTypes<T>(this IQueryable<T> queryable, string propertyName, object propertyValue)
        {
            var classProperty = typeof(T).GetProperties().First(p => p.Name.ToLower() == propertyName);

            var toLowerMethod = typeof(string).GetMethods().FirstOrDefault(m => m.Name == "ToLower" && !m.GetParameters().Any());
            var containsMethod = "".GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == "Contains" && m.GetParameters().Any(p => p.ParameterType == typeof(string)));
            var property = Expression.Parameter(typeof(T), "obj");

            if (classProperty.PropertyType == typeof(string))
            {
                var lowerCaseProperty = Expression.Call(Expression.Property(property, propertyName), toLowerMethod);
                var lowerCaseValue = Expression.Call(Expression.Constant(propertyValue), toLowerMethod);
                var equality = Expression.Equal(lowerCaseProperty, lowerCaseValue);
                var contains = Expression.Call(lowerCaseProperty, containsMethod, lowerCaseValue);

                var or = Expression.Or(equality, contains);
                queryable = Queryable.Where(queryable, Expression.Lambda<Func<T, bool>>(or, property));
                //queryable = queryable.Where(p => ((string)property.GetValue(p, null)).ToLowerInvariant().Contains(propertyValue));
            }
            else if (classProperty.PropertyType == typeof(DateTimeOffset) ||
                classProperty.PropertyType == typeof(DateTimeOffset?))
            {

                var dateTimeProperty = Expression.Property(property, propertyName);
                var dateExpression = dateTimeProperty
                    .BuildNullableDateTimeExpression((DateTimeOffset)propertyValue);

                var dayEquality = Expression.Equal(dateExpression.DayQuestion, dateExpression.DayAnswer);
                var monthEquality = Expression.Equal(dateExpression.MonthQuestion, dateExpression.MonthAnswer);
                var yearEquality = Expression.Equal(dateExpression.YearQuestion, dateExpression.YearAnswer);

                var and = Expression.And(dayEquality, monthEquality);
                and = Expression.And(and, yearEquality);

                var finalExpression = Expression.AndAlso(dateExpression.HasValueExpression, and);

                queryable = Queryable.Where(queryable, Expression.Lambda<Func<T, bool>>(finalExpression, property));

            }
            else if (classProperty.PropertyType == typeof(DateTime) ||
                classProperty.PropertyType == typeof(DateTime?))
            {

                var dateTimeProperty = Expression.Property(property, propertyName);
                var dateExpression = dateTimeProperty
                    .BuildNullableDateTimeExpression((DateTime)propertyValue);

                var dayEquality = Expression.Equal(dateExpression.DayQuestion, dateExpression.DayAnswer);
                var monthEquality = Expression.Equal(dateExpression.MonthQuestion, dateExpression.MonthAnswer);
                var yearEquality = Expression.Equal(dateExpression.YearQuestion, dateExpression.YearAnswer);

                var and = Expression.And(dayEquality, monthEquality);
                and = Expression.And(and, yearEquality);

                var finalExpression = Expression.AndAlso(dateExpression.HasValueExpression, and);

                queryable = Queryable.Where(queryable, Expression.Lambda<Func<T, bool>>(finalExpression, property));

            }
            else
            {
                var equality = Expression.Equal(Expression.Property(property, propertyName), Expression.Constant(propertyValue));
                queryable = Queryable.Where(queryable, Expression.Lambda<Func<T, bool>>(equality, property));
            }

            return queryable;
        }
        public record DateExpression(
        Expression DayQuestion,
        Expression DayAnswer,
        Expression MonthQuestion,
        Expression MonthAnswer,
        Expression YearQuestion,
        Expression YearAnswer,
        Expression HasValueExpression);
        public static DateExpression BuildNullableDateTimeExpression(
            this Expression dateTimeProperty,
            DateTimeOffset value)
        {
            var hasValueExpression = Expression.Property(dateTimeProperty, "HasValue");
            var valueAccessExpression = Expression.Property(dateTimeProperty, "Value");

            var qDay = Expression.PropertyOrField(valueAccessExpression, "Day");
            var qMonth = Expression.PropertyOrField(valueAccessExpression, "Month");
            var qYear = Expression.PropertyOrField(valueAccessExpression, "Year");

            var aDay = Expression.Constant(value.Day);
            var aMonth = Expression.Constant(value.Month);
            var aYear = Expression.Constant(value.Year);

            return new DateExpression(qDay,
                aDay,
                qMonth,
                aMonth,
                qYear,
                aYear,
                hasValueExpression);
        }

        /// <summary>
        /// This method includes all there is to include to join all tables that are joinable and pull a
        /// full state
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="queryable"></param>
        /// <returns></returns>
        public static IQueryable<T> IncludeAll<T>(this IQueryable<T> queryable) where T : class
        {
            try
            {
                var type = typeof(T);
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p =>
                        !p.PropertyType.IsValueType &&
                        p.PropertyType != typeof(Guid) &&
                        p.PropertyType != typeof(string) &&
                        !p.PropertyType.IsPrimitive);
                foreach (var property in properties)
                {
                    var _propertyType = property.PropertyType;

                    queryable = IncludeRecursively(queryable, _propertyType, type, property.Name, type.Name);

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            return queryable;
        }

        private static IQueryable<T> IncludeRecursively<T>(IQueryable<T> queryable, Type propertyType, Type baseType, string propertyName, string basePropertyName) where T : class
        {
            var isEnumerableType = propertyType.GetInterface(nameof(IEnumerable)) != null;
            var genericArgs = propertyType.GetGenericArguments();
            IEnumerable<PropertyInfo> _colProperties = new List<PropertyInfo>();
            Type collection = null;
            if (isEnumerableType && genericArgs.Length == 1)
            {
                collection = genericArgs[0];

                _colProperties = collection.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p =>
                        !p.PropertyType.IsValueType &&
                        p.PropertyType != typeof(Guid) &&
                        p.PropertyType != typeof(string) &&
                        !p.PropertyType.IsPrimitive);

                if (collection != typeof(Guid) &&
                    collection != typeof(string) &&
                    propertyName != nameof(StateBase.DeletedEntities))
                {
                    queryable = queryable.Include(propertyName);
                }

            }
            else if (propertyType.IsAssignableTo(typeof(StateBase)))
            {
                if (!basePropertyName.Split(".")[0].Contains(propertyType.Name) && propertyName != nameof(StateBase.DeletedEntities))
                {
                    collection = propertyType;
                    queryable = queryable.Include(propertyName);

                    _colProperties = propertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p =>
                            !p.PropertyType.IsValueType &&
                            p.PropertyType != typeof(Guid) &&
                            p.PropertyType != typeof(string) &&
                            !p.PropertyType.IsPrimitive);
                }
            }

            foreach (var colProp in _colProperties)
            {
                if (colProp.Name.Contains(basePropertyName) ||
                    (colProp.PropertyType.IsGenericType &&
                    (colProp.PropertyType.GenericTypeArguments[0] == typeof(Guid) ||
                    colProp.PropertyType.GenericTypeArguments[0] == typeof(string) ||
                    colProp.PropertyType.GenericTypeArguments[0] == typeof(Byte) ||
                    colProp.PropertyType.GenericTypeArguments[0] == typeof(bool) ||
                    colProp.PropertyType.GenericTypeArguments[0] == typeof(int) ||
                    !colProp.PropertyType.GenericTypeArguments[0].IsAssignableTo(typeof(StateBase)))))
                {
                    continue;
                }

                queryable = IncludeRecursively<T>(queryable, colProp.PropertyType, collection, $"{propertyName}.{colProp.Name}", propertyName);
            }

            return queryable;
        }
    }
}
