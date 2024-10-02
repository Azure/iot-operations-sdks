namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Text;

    public static class NamingSupport
    {
        public static string ToSnakeCase(string propertyName)
        {
            StringBuilder fieldName = new();

            int lower_ix = 0;

            while (lower_ix < propertyName.Length)
            {
                // copy everything until the next lowercase letter
                while (lower_ix < propertyName.Length && !char.IsLower(propertyName[lower_ix]))
                {
                    fieldName.Append(char.ToLower(propertyName[lower_ix]));
                    ++lower_ix;
                }

                // find the next uppercase letter
                int upper_ix = lower_ix;
                while (upper_ix < propertyName.Length && !char.IsUpper(propertyName[upper_ix]))
                {
                    ++upper_ix;
                }

                // if no uppercase, or if there is intervening underscore, just copy
                if (upper_ix == propertyName.Length || propertyName.Substring(lower_ix, upper_ix - lower_ix).Contains('_'))
                {
                    fieldName.Append(propertyName.Substring(lower_ix, upper_ix - lower_ix));
                }
                // otherwise, copy but insert an underscore just after the last lowercase letter
                else
                {
                    int last_lower_ix = upper_ix - 1;
                    while (!char.IsLower(propertyName[last_lower_ix]))
                    {
                        --last_lower_ix;
                    }

                    fieldName.Append(propertyName.Substring(lower_ix, last_lower_ix - lower_ix + 1));
                    fieldName.Append('_');
                    fieldName.Append(propertyName.Substring(last_lower_ix + 1, upper_ix - last_lower_ix - 1));
                }

                lower_ix = upper_ix;
            }

            return fieldName.ToString();
        }
    }
}
