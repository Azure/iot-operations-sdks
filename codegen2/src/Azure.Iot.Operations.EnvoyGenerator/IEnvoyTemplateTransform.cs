namespace Azure.Iot.Operations.EnvoyGenerator
{
    internal interface IEnvoyTemplateTransform
    {
        string FileName { get; }

        string FolderPath { get; }

        string TransformText();
    }
}
