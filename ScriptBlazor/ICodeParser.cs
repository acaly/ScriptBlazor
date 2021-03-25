using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor
{
    //Just prevent passing random objects as type info.
    public interface IComponentTypeInfo
    {
    }

    //Read input and convert code parts into IParsedTemplateObject
    //This interface knows about the input language (and how to generate a IParsedTemplateObject
    //that writes to ICodeGenerator, which probably means the grammar of output language)
    public interface ICodeParser
    {
        IParsedTemplateObject ParseCodeBlock(PeekableTokenSequence input, IFragmentParser fragmentParser);
        IParsedExpressionObject ParseExpression(PeekableTokenSequence input, IFragmentParser fragmentParser);

        //Return a tag type object used in ParseAttributeValue.
        IComponentTypeInfo ParseTagName(string name);

        //Read the entire attribute value and generate a IParsedTemplateObject.
        //The callee can change the attribute name added to the recorder.
        IParsedExpressionObject ParseAttributeExpression(PeekableTokenSequence input, IFragmentParser fragmentParser,
            IComponentTypeInfo tagType, ref string attributeName);
    }
}
