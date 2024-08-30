namespace TestEnvoys
{
    using Avro;
    using Avro.Specific;

    public class EmptyAvro : ISpecificRecord
    {
        public Schema Schema { get => PrimitiveSchema.Create(Schema.Type.Null); }
        public object Get(int fieldPos) { return null!; }
        public void Put(int fieldPos, object fieldValue) { }
    }
}
