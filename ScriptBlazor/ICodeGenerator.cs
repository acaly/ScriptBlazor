using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor
{
    //From instructions to blazor code.
    //This interface knows about the output language.
    public interface ICodeGenerator
    {
        void WriteRaw(string rawCode);

        void WriteExpression(int nestLevel, ref int sequence, IParsedExpressionObject expr);
        void WriteExpression(string stringLiteral);

        void WriteContent(int nestLevel, ref int sequence, IParsedExpressionObject expr);
        void WriteMarkupContent(int nestLevel, int sequence, string str);

        void OpenElement(int nestLevel, int sequence, string elementName);
        void CloseElement(int nestLevel);
        void OpenComponent(int nestLevel, ref int sequence, IComponentTypeInfo componentType);
        void CloseComponent(int nestLevel);
        void Attribute(int nestLevel, ref int sequence, string attributeName, IParsedExpressionObject value);
        void OpenRegion(int nestLevel, int sequence);
        void CloseRegion(int nestLevel);

        void BeginCodeBlock();
        void EndCodeBlock();
    }
}
