using System.Reflection;
using AngleSharp.Dom;

namespace EPubBook;

public static class ElementExtensions
{
    public static void AsSelfClosing(this IElement element)
    {
        const int SelfClosing = 0x1;

        var type = typeof(IElement).Assembly.GetType("AngleSharp.Dom.Node");
        var field = type.GetField("_flags", BindingFlags.Instance | BindingFlags.NonPublic);

        var flags = (uint)field.GetValue(element);
        flags |= SelfClosing;
        field.SetValue(element, Enum.ToObject(field.FieldType, flags));
    }
}