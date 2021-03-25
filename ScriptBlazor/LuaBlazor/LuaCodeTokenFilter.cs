using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor.LuaBlazor
{
    public sealed class LuaCodeTokenFilter : ITokenFilter
    {
        public static readonly LuaCodeTokenFilter Instance = new();

        private LuaCodeTokenFilter()
        {
        }

        public TokenFilterAction FilterToken(TokenFilterEnv env, PeekableTokenSequence tokenSequence)
        {
            //Here we use a simple rule: @ + keywords = block, @ + others = expr.
            //This will allow starting block inside attribute values, which will throw in fragment parser.
            var (type, content) = tokenSequence.Current;
            if (type != TemplateTokenizer.TokenType.Symbols)
            {
                return TokenFilterAction.None;
            }
            var atIndex = content.IndexOf('@');
            if (atIndex == -1)
            {
                return TokenFilterAction.None;
            }
            if (atIndex != 0)
            {
                //Stop before the '@'.
                tokenSequence.SplitCurrent(atIndex);
                return TokenFilterAction.None;
            }

            //OK we are at the '@'.
            if (content.Length > 1 && content[1] == '@')
            {
                //Escape @@ => @. Consume the first one.
                tokenSequence.SplitCurrent(1);
                tokenSequence.EnsureMoveNext();

                //Read the second as normal text.
                tokenSequence.SplitCurrent(1);
                return TokenFilterAction.None;
            }
            else if (content.Length == 1 && tokenSequence.TryPeek(1, out var nextToken) && IsKeyword(nextToken))
            {
                //The next token is a keyword. Read as block.
                tokenSequence.EnsureMoveNext();
                return TokenFilterAction.BeginScriptBlock;
            }
            else
            {
                //It's an expression. Consume the '@'.
                tokenSequence.SplitCurrent(1);
                tokenSequence.EnsureMoveNext();

                //Now start the rest as an expression.
                return TokenFilterAction.BeginScriptExpression;
            }
        }

        private static bool IsKeyword(TemplateTokenizer.Token token)
        {
            if (token.Type != TemplateTokenizer.TokenType.Text) return false;
            return _luaKeywords.Contains(token.Content.ToString());
        }

        private readonly static HashSet<string> _luaKeywords = new()
        {
            "code",

            //This is not a complete list, because not all keywords can be used to lead a block.
            "do",
            "end",
            "for",
            "function",
            "if",
            "local",
            "while",
            "repeat",
        };
    }
}
