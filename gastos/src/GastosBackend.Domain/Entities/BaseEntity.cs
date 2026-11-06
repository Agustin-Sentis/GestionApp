namespace GastosBackend.Domain.Entities
{
    public abstract class BaseEntity
    {
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        /// <summary>Set on insert; used as a tiebreaker when ordering records that share the same date.</summary>
        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset UpdateDate { get; set; }

        /// <summary>Soft delete: DateTimeOffset.MinValue (default) mientras el registro no esté borrado.</summary>
        public DateTimeOffset DeleteDate { get; set; }
    }
}
