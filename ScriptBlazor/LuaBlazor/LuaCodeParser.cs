using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor.LuaBlazor
{
    public sealed class LuaCodeParser : ICodeParser
    {
        public static readonly LuaCodeParser Instance = new();

        private LuaCodeParser()
        {
        }

        public IParsedTemplateObject ParseCodeBlock(PeekableTokenSequence input, IFragmentParser fragmentParser)
        {
            if (input.Current.Type == TemplateTokenizer.TokenType.Text &&
                MemoryExtensions.Equals(input.Current.Content, "code", StringComparison.Ordinal))
            {
                //TODO need to ensure we are not inside another Lua block.
                //Special handling of @code block
                input.EnsureMoveNext();
                return LuaParser.ParseCodeBlock(input);
            }
            return LuaParser.ParseStatement(input);
        }

        public IParsedExpressionObject ParseExpression(PeekableTokenSequence input, IFragmentParser fragmentParser)
        {
            return LuaParser.ParseExpr(input);
        }

        public IComponentTypeInfo ParseTagName(string name)
        {
            //Whether this name is a component type?
            return null;
        }

        public IParsedExpressionObject ParseAttributeExpression(PeekableTokenSequence input,
            IFragmentParser fragmentParser, IComponentTypeInfo tagType, ref string attributeName)
        {
            //Handle special attribute names.
            switch (attributeName)
            {
            case "@onclick":
                attributeName = "onclick";
                return ParseAttrLimited(input, fragmentParser);
            default:
                break;
            }

            if (tagType is not null)
            {
                //A component's attribute.
                //No support for component yet.
                throw new NotSupportedException();
            }

            //A normal expression inside a normal attribute.
            return ParseAttrLimited(input, fragmentParser);
        }

        private IParsedExpressionObject ParseAttrLimited(PeekableTokenSequence input, IFragmentParser fragmentParser)
        {
            var (peek, pos) = FindAttrEnd(input);
            if (pos > 0)
            {
                input.Split(peek, pos);
                input.SetPeekLimit(peek + 1);
            }
            else
            {
                input.SetPeekLimit(peek);
            }
            var ret = ParseExpression(input, fragmentParser);
            input.ClearPeekLimit();
            //Because of the limit, the last Lua token was not removed from input.
            //See comment in LuaTokenizer.MoveNext().
            input.EnsureMoveNext();
            return ret;
        }

        private static (int peek, int pos) FindAttrEnd(PeekableTokenSequence input)
        {
            int peek = 0;
            while (true)
            {
                if (!input.TryPeek(peek, out var token))
                {
                    //Can't find the end of the attribute before EOS.
                    throw new Exception("Invalid attribute value");
                }
                if (token.Type == TemplateTokenizer.TokenType.Symbols)
                {
                    var index = token.Content.IndexOf('"');
                    if (index != -1)
                    {
                        return (peek, index);
                    }
                }
                peek += 1;
            }
        }
    }
}
