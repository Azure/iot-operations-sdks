namespace Azure.Iot.Operations.Protocol
{
    using System;

    [AttributeUsage(AttributeTargets.Class)]
    public class CommandBehaviorAttribute : Attribute
    {
        public bool IsIdempotent { get; set; }

        public string CacheTtl { get; set; }

        public CommandBehaviorAttribute(bool idempotent = false, string cacheTtl = "PT0H0M0S")
        {
            this.IsIdempotent = idempotent;
            this.CacheTtl = cacheTtl;
        }
    }
}
