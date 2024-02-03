using System.ComponentModel;
using System.Reflection;

namespace Bot;

public class EnumHelpers
{
    public static string GetEnumDescription(Enum value)
    {
        FieldInfo? fi = value.GetType().GetField(value.ToString());

        if (fi == null)
            throw new ArgumentException("Cant get fieldDescription from enum");
        
        DescriptionAttribute[]? attributes = fi.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];

        if (attributes != null && attributes.Any())
        {
            return attributes.First().Description;
        }

        return value.ToString();
    }
}