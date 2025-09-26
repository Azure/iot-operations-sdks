namespace Azure.Iot.Operations.SchemaGenerator
{
    internal interface ISchemaTemplateTransform
    {
        string FileName { get; }

        string FolderPath { get; }

        string TransformText();
    }
}
