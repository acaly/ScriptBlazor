using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor.LuaBlazor
{
    //Lua 5.2 parser (without backend) to correctly handle Lua code.
    internal class LuaParser
    {
        private delegate void WriteDelegate(ICodeGenerator g, int n, ref int s);

        private class DelegateParsedObject : IParsedTemplateObject, IParsedExpressionObject
        {
            public WriteDelegate Value { get; init; }

            public void WriteToOutput(ICodeGenerator writer, int nestLevel, ref int sequence)
            {
                Value(writer, nestLevel, ref sequence);
            }
        }

        private class TagParsedObject : IParsedExpressionObject
        {
            public IParsedTemplateObject Tag { get; init; }

            public void WriteToOutput(ICodeGenerator writer, int nestLevel, ref int sequence)
            {
                writer.WriteRaw($"function(__builder{nestLevel + 1})\n");
                Tag.WriteToOutput(writer, nestLevel + 1, ref sequence);
                writer.WriteRaw($"end");
            }
        }

        private class CodeBlockParsedObject : IParsedTemplateObject
        {
            public IParsedTemplateObject Content { get; init; }

            public void WriteToOutput(ICodeGenerator writer, int nestLevel, ref int sequence)
            {
                if (nestLevel != 0)
                {
                    throw new InvalidOperationException();
                }
                writer.BeginCodeBlock();
                int s = 0;
                //nestLevel of -1 disable __builder variable until entering next level.
                Content.WriteToOutput(writer, -1, ref s);
                writer.EndCodeBlock();
            }
        }

        private PeekableTokenSequence RawTokenizer { get; init; }
        private LuaTokenizer Tokenizer { get; init; }
        private FragmentRecorder OutputFrag { get; init; }
        private ExpressionRecorder OutputExpr { get; init; }
        private StringBuilder TextCodeBuffer { get; init; }
        private readonly List<bool> TextBufferContainsExprStack = new();

        private LuaParser()
        {
        }

        public static IParsedExpressionObject ParseExpr(PeekableTokenSequence input)
        {
            var sb = new StringBuilder();
            var recorder = new ExpressionRecorder();
            var parser = new LuaParser()
            {
                RawTokenizer = input,
                Tokenizer = new(input, sb),
                OutputExpr = recorder,
                TextCodeBuffer = sb,
            };
            parser.TextBufferContainsExprStack.Add(true);
            parser.ParseMixedExpr();
            parser.FlushTextBuffer();
            return recorder.ToParsedObject();
        }

        public static IParsedTemplateObject ParseStatement(PeekableTokenSequence input)
        {
            var sb = new StringBuilder();
            var recorder = new FragmentRecorder();
            var parser = new LuaParser()
            {
                RawTokenizer = input,
                Tokenizer = new(input, sb),
                OutputFrag = recorder,
                TextCodeBuffer = sb,
            };
            parser.TextBufferContainsExprStack.Add(false);
            parser.ParseMixedStatement();
            parser.FlushTextBuffer();
            return recorder.ToParsedObject();
        }

        //@code block. The code has been consumed from input.
        public static IParsedTemplateObject ParseCodeBlock(PeekableTokenSequence input)
        {
            var sb = new StringBuilder();
            var recorder = new FragmentRecorder();
            var parser = new LuaParser()
            {
                RawTokenizer = input,
                Tokenizer = new(input, sb),
                OutputFrag = recorder,
                TextCodeBuffer = sb,
            };
            parser.TextBufferContainsExprStack.Add(false);
            parser.Block();
            parser.FlushTextBuffer();
            var ret = new CodeBlockParsedObject { Content = recorder.ToParsedObject() };
            parser.CheckAndNext(LuaTokenizer.TokenType.End);
            return ret;
        }

        private void FlushTextBuffer()
        {
            if (TextCodeBuffer.Length > 0)
            {
                TextCodeBuffer.AppendLine();
                var str = TextCodeBuffer.ToString();
                TextCodeBuffer.Clear();
                var obj = new DelegateParsedObject()
                {
                    Value = (ICodeGenerator g, int n, ref int s) =>
                    {
                        g.WriteRaw(str);
                    },
                };
                if (TextBufferContainsExprStack[^1])
                {
                    OutputExpr.Write(obj);
                }
                else
                {
                    OutputFrag.Write(obj);
                }
            }
        }

        private void ParseMixedExpr()
        {
            if (Tokenizer.Current == (LuaTokenizer.TokenType)'@')
            {
                Tokenizer.Detach();
                FlushTextBuffer();

                var rawContent = RawTokenizer.Current.Content;
                if (rawContent.Length == 1 || rawContent[1] != '<')
                {
                    //We assume that @ always starts a html tag.
                    throw new Exception("Invalid embedded tag element");
                }

                //Skip the '@' only.
                RawTokenizer.SplitCurrent(1);
                RawTokenizer.EnsureMoveNext();

                var tagRecorder = new FragmentRecorder();
                HtmlFragmentParser.Instance.Parse(RawTokenizer, LuaCodeTokenFilter.Instance,
                    LuaCodeParser.Instance, tagRecorder);

                OutputExpr.Write(new TagParsedObject { Tag = tagRecorder.ToParsedObject() });

                //Reconnect.
                Tokenizer.Reset();
            }
            else
            {
                ParseLuaExpr();
            }
        }

        private void ParseLuaExpr()
        {
            //Top-level lua expr is limited to simpleexpr.
            SimpleExpr(false);
        }

        private void ParseMixedStatement()
        {
            if (Tokenizer.Current == (LuaTokenizer.TokenType)'<')
            {
                Tokenizer.Detach();
                FlushTextBuffer();

                var tagRecorder = new FragmentRecorder();
                HtmlFragmentParser.Instance.Parse(RawTokenizer, LuaCodeTokenFilter.Instance,
                    LuaCodeParser.Instance, tagRecorder);

                OutputFrag.WriteChildObj(tagRecorder.ToParsedObject());

                //Reconnect.
                Tokenizer.Reset();
            }
            else
            {
                Statement();
            }
        }

        private void CheckAndNext(LuaTokenizer.TokenType token)
        {
            if (Tokenizer.Current != token)
            {
                if (token < LuaTokenizer.TokenType.First)
                {
                    throw new Exception("Expecting " + (char)token);
                }
                else
                {
                    throw new Exception("Expecting " + token);
                }
            }
            Tokenizer.EnsumeMoveNext();
        }

        #region expr

        private bool IsUnOpr()
        {
            return Tokenizer.Current switch
            {
                LuaTokenizer.TokenType.Not => true,
                (LuaTokenizer.TokenType)'-' => true,
                (LuaTokenizer.TokenType)'#' => true,
                _ => false,
            };
        }

        private bool IsBinOpr()
        {
            return Tokenizer.Current switch
            {
                (LuaTokenizer.TokenType)'+' => true,
                (LuaTokenizer.TokenType)'-' => true,
                (LuaTokenizer.TokenType)'*' => true,
                (LuaTokenizer.TokenType)'/' => true,
                (LuaTokenizer.TokenType)'%' => true,
                (LuaTokenizer.TokenType)'^' => true,
                LuaTokenizer.TokenType.Concat => true,
                LuaTokenizer.TokenType.Ne => true,
                LuaTokenizer.TokenType.Eq => true,
                (LuaTokenizer.TokenType)'<' => true,
                LuaTokenizer.TokenType.Le => true,
                (LuaTokenizer.TokenType)'>' => true,
                LuaTokenizer.TokenType.Ge => true,
                LuaTokenizer.TokenType.And => true,
                LuaTokenizer.TokenType.Or => true,
                _ => false,
            };
        }

        private void Field()
        {
            switch (Tokenizer.Current)
            {
            case LuaTokenizer.TokenType.Name:
            {
                //We don't implement the peek in LuaTokenizer.
                //So we have to add a flag in Expr method to indicate whether
                //there is a Name token already consumed.
                Tokenizer.EnsumeMoveNext();
                if (Tokenizer.Current == (LuaTokenizer.TokenType)'=')
                {
                    Tokenizer.EnsumeMoveNext();
                    Expr(false);
                }
                else
                {
                    Expr(true);
                }
                break;
            }
            case (LuaTokenizer.TokenType)'[':
            {
                Tokenizer.EnsumeMoveNext();
                Expr(false);
                CheckAndNext((LuaTokenizer.TokenType)']');
                CheckAndNext((LuaTokenizer.TokenType)'=');
                Expr(false);
                break;
            }
            default:
                Expr(false);
                break;
            }
        }

        private void Constructor()
        {
            CheckAndNext((LuaTokenizer.TokenType)'{');
            while (true)
            {
                if (Tokenizer.Current == (LuaTokenizer.TokenType)'}')
                {
                    break;
                }
                Field();
                if (Tokenizer.Current == (LuaTokenizer.TokenType)',' ||
                    Tokenizer.Current == (LuaTokenizer.TokenType)';')
                {
                    Tokenizer.EnsumeMoveNext();
                }
                else
                {
                    break;
                }
            }
            CheckAndNext((LuaTokenizer.TokenType)'}');
        }

        private void ParList()
        {
            if (Tokenizer.Current == (LuaTokenizer.TokenType)')')
            {
                return;
            }
            while (true)
            {
                switch (Tokenizer.Current)
                {
                case LuaTokenizer.TokenType.Name:
                    Tokenizer.EnsumeMoveNext();
                    break;
                case LuaTokenizer.TokenType.Dots:
                    Tokenizer.EnsumeMoveNext();
                    return;
                default:
                    throw new Exception("Expecting parameter name");
                }
                if (Tokenizer.Current != (LuaTokenizer.TokenType)',')
                {
                    return;
                }
                Tokenizer.EnsumeMoveNext();
            }
        }

        private void Body()
        {
            CheckAndNext((LuaTokenizer.TokenType)'(');
            ParList();
            CheckAndNext((LuaTokenizer.TokenType)')');
            StatList();
            CheckAndNext(LuaTokenizer.TokenType.End);
        }

        private void ExprList()
        {
            Expr(false);
            while (Tokenizer.Current == (LuaTokenizer.TokenType)',')
            {
                Tokenizer.EnsumeMoveNext();
                Expr(false);
            }
        }

        private void FuncArgs()
        {
            switch (Tokenizer.Current)
            {
            case (LuaTokenizer.TokenType)'(':
                Tokenizer.EnsumeMoveNext();
                if (Tokenizer.Current == (LuaTokenizer.TokenType)')')
                {
                    Tokenizer.EnsumeMoveNext();
                }
                else
                {
                    ExprList();
                    CheckAndNext((LuaTokenizer.TokenType)')');
                }
                break;
            case (LuaTokenizer.TokenType)'{':
                Constructor();
                break;
            case LuaTokenizer.TokenType.String:
                Tokenizer.EnsumeMoveNext();
                break;
            default:
                throw new Exception("Expecting function arguments");
            }
        }

        private void PrimaryExpr(bool nameSkipped)
        {
            if (nameSkipped)
            {
                return;
            }
            switch (Tokenizer.Current)
            {
            case (LuaTokenizer.TokenType)'(':
                Tokenizer.EnsumeMoveNext();
                Expr(false);
                CheckAndNext((LuaTokenizer.TokenType)')');
                break;
            case LuaTokenizer.TokenType.Name:
                Tokenizer.EnsumeMoveNext();
                break;
            default:
                throw new Exception("Unexpected symbol");
            }
        }

        private void SuffixedExpr(bool nameSkipped)
        {
            PrimaryExpr(nameSkipped);
            while (true)
            {
                switch (Tokenizer.Current)
                {
                case (LuaTokenizer.TokenType)'.':
                    Tokenizer.EnsumeMoveNext();
                    CheckAndNext(LuaTokenizer.TokenType.Name);
                    break;
                case (LuaTokenizer.TokenType)'[':
                    Tokenizer.EnsumeMoveNext();
                    Expr(false);
                    CheckAndNext((LuaTokenizer.TokenType)']');
                    break;
                case (LuaTokenizer.TokenType)':':
                    Tokenizer.EnsumeMoveNext();
                    CheckAndNext(LuaTokenizer.TokenType.Name);
                    FuncArgs();
                    break;
                case (LuaTokenizer.TokenType)'(':
                case LuaTokenizer.TokenType.String:
                case (LuaTokenizer.TokenType)'{':
                    FuncArgs();
                    break;
                default:
                    return;
                }
            }
        }

        private void SimpleExpr(bool nameSkipped)
        {
            switch (Tokenizer.Current)
            {
            case LuaTokenizer.TokenType.Number:
            case LuaTokenizer.TokenType.String:
            case LuaTokenizer.TokenType.Nil:
            case LuaTokenizer.TokenType.True:
            case LuaTokenizer.TokenType.False:
            case LuaTokenizer.TokenType.Dots:
                Tokenizer.EnsumeMoveNext();
                break;
            case (LuaTokenizer.TokenType)'{':
                Constructor();
                break;
            case LuaTokenizer.TokenType.Function:
                Tokenizer.EnsumeMoveNext(); //Skip "function".
                Body();
                break;
            default:
                SuffixedExpr(nameSkipped);
                break;
            }
        }

        private void SubExpr(bool nameSkipped)
        {
            if (!nameSkipped && IsUnOpr())
            {
                Tokenizer.EnsumeMoveNext();
                //In Lua, this is a subexpr with priority limited to 8.
                //We don't care about semantics, so simpleexpr is enough.
            }
            SimpleExpr(nameSkipped);
            while (IsBinOpr())
            {
                Tokenizer.EnsumeMoveNext();
                SubExpr(false);
            }
        }

        private void Expr(bool nameSkipped)
        {
            if (Tokenizer.Current == (LuaTokenizer.TokenType)'@')
            {
                ParseLuaExpr();
            }
            else
            {
                SubExpr(nameSkipped);
            }
        }

        #endregion

        #region stat

        private bool BlockFollow(bool withUntil)
        {
            return Tokenizer.Current switch
            {
                LuaTokenizer.TokenType.Else => true,
                LuaTokenizer.TokenType.Elseif => true,
                LuaTokenizer.TokenType.End => true,
                LuaTokenizer.TokenType.Eos => true,
                LuaTokenizer.TokenType.Until => withUntil,
                _ => false,
            };
        }

        private void Block()
        {
            StatList();
        }

        private void ThenBlock()
        {
            Expr(false);
            CheckAndNext(LuaTokenizer.TokenType.Then);
            StatList();
        }

        private void IfStat()
        {
            Tokenizer.EnsumeMoveNext();
            ThenBlock();
            while (Tokenizer.Current == LuaTokenizer.TokenType.Elseif)
            {
                Tokenizer.EnsumeMoveNext();
                ThenBlock();
            }
            if (Tokenizer.Current == LuaTokenizer.TokenType.Else)
            {
                Block();
            }
            CheckAndNext(LuaTokenizer.TokenType.End);
        }

        private void WhileStat()
        {
            Tokenizer.EnsumeMoveNext();
            Expr(false);
            CheckAndNext(LuaTokenizer.TokenType.Do);
            Block();
            CheckAndNext(LuaTokenizer.TokenType.End);
        }

        private void ForBody()
        {
            CheckAndNext(LuaTokenizer.TokenType.Do);
            Block();
            CheckAndNext(LuaTokenizer.TokenType.End);
        }

        private void ForNum()
        {
            CheckAndNext((LuaTokenizer.TokenType)'=');
            Expr(false);
            CheckAndNext((LuaTokenizer.TokenType)',');
            Expr(false);
            if (Tokenizer.Current == (LuaTokenizer.TokenType)',')
            {
                Tokenizer.EnsumeMoveNext();
                Expr(false);
            }
            ForBody();
        }

        private void ForList()
        {
            while (Tokenizer.Current == (LuaTokenizer.TokenType)',')
            {
                Tokenizer.EnsumeMoveNext();
                CheckAndNext(LuaTokenizer.TokenType.Name);
            }
            CheckAndNext(LuaTokenizer.TokenType.In);
            ExprList();
            ForBody();
        }

        private void ForStat()
        {
            Tokenizer.EnsumeMoveNext();
            CheckAndNext(LuaTokenizer.TokenType.Name);
            switch (Tokenizer.Current)
            {
            case (LuaTokenizer.TokenType)'=':
                ForNum();
                break;
            case (LuaTokenizer.TokenType)',':
            case LuaTokenizer.TokenType.In:
                ForList();
                break;
            default:
                throw new Exception("Expecting '=' or 'in'");
            }
        }

        private void RepeatStat()
        {
            Tokenizer.EnsumeMoveNext();
            StatList();
            CheckAndNext(LuaTokenizer.TokenType.Until);
            Expr(false);
        }

        private void FuncName()
        {
            CheckAndNext(LuaTokenizer.TokenType.Name);
            while (Tokenizer.Current == (LuaTokenizer.TokenType)'.')
            {
                Tokenizer.EnsumeMoveNext();
                CheckAndNext(LuaTokenizer.TokenType.Name);
            }
            if (Tokenizer.Current == (LuaTokenizer.TokenType)':')
            {
                Tokenizer.EnsumeMoveNext();
                CheckAndNext(LuaTokenizer.TokenType.Name);
            }
        }

        private void FuncStat()
        {
            Tokenizer.EnsumeMoveNext();
            FuncName();
            Body();
        }

        private void LocalStat()
        {
            //This is different from Lua. We don't have peek, so the local
            //keyword has been consumed when entering this method.
            //CheckAndNext(LuaTokenizer.TokenType.Local);
            CheckAndNext(LuaTokenizer.TokenType.Name);
            while (Tokenizer.Current == (LuaTokenizer.TokenType)',')
            {
                Tokenizer.EnsumeMoveNext();
                CheckAndNext(LuaTokenizer.TokenType.Name);
            }
            if (Tokenizer.Current == (LuaTokenizer.TokenType)'=')
            {
                Tokenizer.EnsumeMoveNext();
                ExprList();
            }
        }

        private void LocalFuncStat()
        {
            CheckAndNext(LuaTokenizer.TokenType.Function);
            CheckAndNext(LuaTokenizer.TokenType.Name);
            Body();
        }

        private void LabelStat()
        {
            CheckAndNext(LuaTokenizer.TokenType.Dbcolon);
            CheckAndNext(LuaTokenizer.TokenType.Name);
            CheckAndNext(LuaTokenizer.TokenType.Dbcolon);
        }

        private void RetStat()
        {
            CheckAndNext(LuaTokenizer.TokenType.Return);
            if (BlockFollow(withUntil: true) || Tokenizer.Current == (LuaTokenizer.TokenType)';')
            {
            }
            else
            {
                ExprList();
            }
            if (Tokenizer.Current == (LuaTokenizer.TokenType)';')
            {
                Tokenizer.EnsumeMoveNext();
            }
        }

        private void GotoBreakStat()
        {
            if (Tokenizer.Current == LuaTokenizer.TokenType.Goto)
            {
                Tokenizer.EnsumeMoveNext();
                CheckAndNext(LuaTokenizer.TokenType.Name);
            }
            else
            {
                CheckAndNext(LuaTokenizer.TokenType.Break);
            }
        }

        private void Assignment()
        {
            if (Tokenizer.Current == (LuaTokenizer.TokenType)',')
            {
                Tokenizer.EnsumeMoveNext();
                SuffixedExpr(false);
                Assignment();
            }
            else
            {
                CheckAndNext((LuaTokenizer.TokenType)'=');
                ExprList();
            }
        }

        private void ExprStat()
        {
            SuffixedExpr(false);
            if (Tokenizer.Current == (LuaTokenizer.TokenType)'=' ||
                Tokenizer.Current == (LuaTokenizer.TokenType)',')
            {
                Assignment();
            }
        }

        private void Statement()
        {
            switch (Tokenizer.Current)
            {
            case (LuaTokenizer.TokenType)';':
                Tokenizer.EnsumeMoveNext();
                break;
            case LuaTokenizer.TokenType.If:
                IfStat();
                break;
            case LuaTokenizer.TokenType.While:
                WhileStat();
                break;
            case LuaTokenizer.TokenType.Do:
                Tokenizer.EnsumeMoveNext();
                Block();
                CheckAndNext(LuaTokenizer.TokenType.End);
                break;
            case LuaTokenizer.TokenType.For:
                ForStat();
                break;
            case LuaTokenizer.TokenType.Repeat:
                RepeatStat();
                break;
            case LuaTokenizer.TokenType.Function:
                FuncStat();
                break;
            case LuaTokenizer.TokenType.Local:
                Tokenizer.EnsumeMoveNext();
                if (Tokenizer.Current == LuaTokenizer.TokenType.Function)
                {
                    LocalFuncStat();
                }
                else
                {
                    LocalStat();
                }
                break;
            case LuaTokenizer.TokenType.Dbcolon:
                LabelStat();
                break;
            case LuaTokenizer.TokenType.Return:
                //TODO should disallow return in some cases
                RetStat();
                break;
            case LuaTokenizer.TokenType.Break:
            case LuaTokenizer.TokenType.Goto:
                GotoBreakStat();
                break;
            default:
                ExprStat();
                break;
            }
        }

        private void StatList()
        {
            while (!BlockFollow(withUntil: true))
            {
                if (Tokenizer.Current == LuaTokenizer.TokenType.Return)
                {
                    Statement();
                    return;
                }
                MixedStatement();
            }
        }

        private void MixedStatement()
        {
            if (Tokenizer.Current == (LuaTokenizer.TokenType)'<')
            {
                ParseMixedStatement();
            }
            else
            {
                Statement();
            }
        }

        #endregion
    }
}
