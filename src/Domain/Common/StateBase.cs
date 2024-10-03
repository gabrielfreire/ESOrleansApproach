using Orleans;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Common
{
    [GenerateSerializer]
    public class StateBase : StateBaseAudit
    {
        [Id(0)]
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public Guid Id { get; set; }
        [Id(1)]
        public bool Deleted { get; set; }
        [Id(2)]
        public int Version { get; set; } = -2;
        [Id(3)]
        public bool Created { get; set; } = false;

        [Id(4)]
        [NotMapped]
        public List<object> DeletedEntities { get; set; } = [];

        /// <summary>
        /// Applies an event to the entity and marks it for update in EF DbContext ChangeTracker
        /// </summary>
        /// <param name="eventBase"></param>
        public void Apply(EventBase eventBase)
        {
            if (Id == Guid.Empty)
            {
                Id = eventBase.AggregateId;
            }

            if (CreatedOnUtc != default)
            {
                UpdatedOnUtc = DateTimeOffset.UtcNow;
            }

            Version = eventBase.Version;
        }

        /// <summary>
        /// The entity must override this function and implement logic to mark
        /// all entity nesting levels for update on the EF DbContext ChangeTracker.
        /// This is only necessary after the 2nd level of nesting
        /// <remarks>
        /// <code>
        /// Example:
        /// Library entity has many books
        /// A book has one author
        /// 1st Level is Library
        /// 2nd Level is book
        /// 3rd level is Author - Author must implement ApplyAllLevels to mark Book for update too
        /// </code>
        /// 
        /// </remarks>
        /// </summary>
        /// <param name="eventBase">The event</param>
        /// <param name="rootEntity">Based on the example the rootEntity can be Book or Library</param>
        public virtual void ApplyAllLevels<T>(EventBase eventBase, T rootEntity) where T : StateBase
        {
        }

        /// <summary>
        /// Use reflection to find the correct event type and state type
        /// and call apply
        /// </summary>
        /// <param name="state"></param>
        /// <param name="event"></param>
        public void CallApply(StateBase state, EventBase @event)
        {
            var apply = state.GetType().GetMethod("Apply", new[] { @event.GetType() });
            apply.Invoke(state, new object[] { @event });
        }
        /// <summary>
        /// Marks the entity to be deleted in EF dbcontext
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        public void Delete<T>(T entity) where T : StateBase
        {
            DeletedEntities.Add(entity);
        }

        public Guid GenerateGuid(string key)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
            return new Guid(hash);
        }
        public override bool Equals(object obj)
        {
            var compareTo = obj as StateBase;

            if (ReferenceEquals(this, compareTo))
            {
                return true;
            }

            if (compareTo is null)
            {
                return false;
            }

            return Id.Equals(compareTo.Id);
        }

        public static bool operator ==(StateBase a, StateBase b)
        {
            if (a is null && b is null)
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            return a.Equals(b);
        }

        public static bool operator !=(StateBase a, StateBase b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return (GetType().GetHashCode() * 907) + Id.GetHashCode();
        }

        public override string ToString()
        {
            return GetType().Name + " [Id=" + Id + "]";
        }
    }
}
