using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor.LuaBlazor
{
    //Lua 5.2 tokenizer to correctly handle Lua code.
    internal sealed class LuaTokenizer
    {
        public enum TokenType
        {
            //We are using UTF16, so starting from 65536 instead of 256.
            First = 65536,
            And, Break,
            Do, Else, Elseif, End, False, For, Function,
            Goto, If, In, Local, Nil, Not, Or, Repeat,
            Return, Then, True, Until, While,

            Concat, Dots, Eq, Ge, Le, Ne, Dbcolon, Eos,
            Number, Name, String,
        }

        private static readonly Dictionary<string, TokenType> _keywords = new()
        {
            { "do", TokenType.Do },
            { "else", TokenType.Else },
            { "elseif", TokenType.Elseif },
            { "end", TokenType.End },
            { "false", TokenType.False },
            { "for", TokenType.For },
            { "function", TokenType.Function },
            { "goto", TokenType.Goto },
            { "if", TokenType.If },
            { "in", TokenType.In },
            { "local", TokenType.Local },
            { "nil", TokenType.Nil },
            { "not", TokenType.Not },
            { "or", TokenType.Or },
            { "repeat", TokenType.Repeat },
            { "return", TokenType.Return },
            { "then", TokenType.Then },
            { "true", TokenType.True },
            { "until", TokenType.Until },
            { "while", TokenType.While },
        };

        private readonly PeekableTokenSequence _input;
        private readonly StringBuilder _identifierBuilder = new();
        private readonly StringBuilder _copyToOutput;

        private TokenType _current;

        //From _currentPeekBegin to _currentPeekEnd is the range of the current token (returned to
        //parser, and stored in _current), waiting for processing from the parser (detach or MoveNext).
        //_currentPeekBegin is needed because we may have skipped some comments before returning
        //the current token. We should not write the comment to output (it's not controlled by parser).
        //_currentChar is the char at _currentPeekEnd, i.e., the first char of next token, used by
        //next MoveNext call. _currentCharType is its type.
        private int _currentPeekBegin = 0;
        private int _currentPeekEnd = 0;
        private char _currentChar;
        private TemplateTokenizer.TokenType _currentCharType;

        public LuaTokenizer(PeekableTokenSequence input, StringBuilder copyToOutput)
        {
            _input = input;
            _copyToOutput = copyToOutput;

            Reset();
        }

        public void Detach()
        {
            //Skip _currentPeekBegin.
            //This prevents comments before a tag to be rendered as content.
            MoveSkipComments();
        }

        public void Reset()
        {
            _currentPeekBegin = _currentPeekEnd = 0;
            (_currentCharType, _currentChar) = PeekChar(0);
            EnsumeMoveNext();
        }

        private (TemplateTokenizer.TokenType t, char c) DoPeekChar(int pos)
        {
            int peek = 0;
            while (_input.TryPeek(peek, out var templateToken))
            {
                if (templateToken.Content.Length > pos)
                {
                    return (templateToken.Type, templateToken.Content[pos]);
                }
                pos -= templateToken.Content.Length;
                peek += 1;
            }
            return default;
        }

        private (TemplateTokenizer.TokenType t, char c) PeekChar(int pos)
        {
            return DoPeekChar(_currentPeekEnd + pos);
        }

        private void CopyTextTokens(StringBuilder buffer)
        {
            int peek = 0;
            int pos = _currentPeekEnd;
            while (_input.TryPeek(peek, out var templateToken))
            {
                if (templateToken.Content.Length > pos)
                {
                    //The first token is ensured to be Text.
                    buffer.Append(templateToken.Content[pos..]);
                    _currentPeekEnd += templateToken.Content.Length - pos;

                    while (_input.TryPeek(++peek, out templateToken) &&
                        templateToken.Type == TemplateTokenizer.TokenType.Text)
                    {
                        buffer.Append(templateToken.Content);
                        _currentPeekEnd += templateToken.Content.Length;
                    }

                    (_currentCharType, _currentChar) = PeekChar(0);
                    return;
                }
                pos -= templateToken.Content.Length;
                peek += 1;
            }
        }

        private char NextChar()
        {
            ++_currentPeekEnd;
            (_currentCharType, _currentChar) = PeekChar(0);
            return _currentChar;
        }

        private void MoveSkipComments()
        {
            _currentPeekEnd -= _currentPeekBegin;
            while (_currentPeekBegin > 0)
            {
                var inputTokenLen = _input.Current.Content.Length;
                if (_currentPeekBegin < inputTokenLen)
                {
                    _input.SplitCurrent(_currentPeekBegin);
                    _currentPeekBegin = 0;
                    break;
                }
                _input.EnsureMoveNext();
                _currentPeekBegin -= inputTokenLen;
            }
        }

        public bool MoveNext()
        {
            //Consume input and copy content to output.

            //Skip _currentPeekBegin
            MoveSkipComments();

            while (_currentPeekEnd > 0)
            {
                if (_currentPeekEnd <= _input.Current.Content.Length)
                {
                    _copyToOutput.Append(_input.Current.Content[.._currentPeekEnd]);

                    _input.SplitCurrent(_currentPeekEnd);
                    _currentPeekEnd = 0;
                    if (!_input.MoveNext())
                    {
                        //No more input.
                        //In the original design, PeekableTokenSequence should give an EOS,
                        //and the LuaTokenizer returns the Lua EOS accordingly.
                        //However, when parsing attribute values, PeekableTokenSequence has
                        //an additional limit (the end of attribute) but is not EOS.
                        //So we have to handle "cannot MoveNext" from input.
                        //Luckily, this seems to be the only place we have to handle this.
                        //See comment in LuaCodeParser.ParseAttrLimited.
                        if (_current == TokenType.Eos)
                        {
                            return false;
                        }
                        _current = TokenType.Eos;
                        return true;
                    }
                    break;
                }
                _copyToOutput.Append(_input.Current.Content);
                _currentPeekEnd -= _input.Current.Content.Length;
                _input.EnsureMoveNext();
            }

            if (_current == TokenType.Eos)
            {
                return false;
            }
            _current = Tokenize();
            return true;
        }

        public void EnsumeMoveNext()
        {
            if (!MoveNext())
            {
                //This is an internal exception.
                throw new Exception();
            }
        }

        public TokenType Current => _current;

        private static TokenType MakeToken(char c)
        {
            return (TokenType)c;
        }

        private TokenType Tokenize()
        {
            while (true)
            {
                switch (_currentChar)
                {
                case '\n':
                case '\r':
                case ' ':
                case '\f':
                case '\t':
                case '\v':
                {
                    NextChar();
                    break;
                }
                case '-': //'-' or '--'
                {
                    if (NextChar() != '-')
                    {
                        //Not a comment.
                        return MakeToken('-');
                    }
                    if (NextChar() == '[')
                    {
                        var sep = SkipSep();
                        if (sep >= 0)
                        {
                            //Long comment.
                            ReadLongString(sep);
                            _currentPeekBegin = _currentPeekEnd; //Set start position.
                            break;
                        }
                    }
                    //Short comment.
                    while (_currentChar != '\r' && _currentChar != '\n' && _currentChar != default)
                    {
                        NextChar();
                    }
                    _currentPeekBegin = _currentPeekEnd; //Set start position.
                    break;
                }
                case '[': //long string or '['
                {
                    var sep = SkipSep();
                    if (sep >= 0)
                    {
                        ReadLongString(sep);
                        return TokenType.String;
                    }
                    else if (sep == -1)
                    {
                        return MakeToken('[');
                    }
                    else
                    {
                        throw new Exception("Invalid long string delimiter");
                    }
                }
                case '=': //'=' or eq
                {
                    if (NextChar() != '=')
                    {
                        return MakeToken('=');
                    }
                    NextChar();
                    return TokenType.Eq;
                }
                case '<': //'<' or le
                {
                    if (NextChar() != '=')
                    {
                        return MakeToken('<');
                    }
                    NextChar();
                    return TokenType.Le;
                }
                case '>': //'>' or ge
                {
                    if (NextChar() != '=')
                    {
                        return MakeToken('>');
                    }
                    NextChar();
                    return TokenType.Ge;
                }
                case '~': //'~' or ne
                {
                    if (NextChar() != '=')
                    {
                        return MakeToken('~');
                    }
                    NextChar();
                    return TokenType.Ne;
                }
                case ':': //':' or dbcolon
                {
                    if (NextChar() != ':')
                    {
                        return MakeToken(':');
                    }
                    NextChar();
                    return TokenType.Dbcolon;
                }
                case '"': //short literal string
                case '\'':
                {
                    ReadString();
                    return TokenType.String;
                }
                case '.': //'.', concat, dots or number
                {
                    var next = PeekChar(1).c;
                    if (next == '.')
                    {
                        //Not a number.
                        NextChar(); //Skip the first '.'.
                        NextChar(); //Skip the second '.'.
                        if (NextChar() != '.')
                        {
                            return TokenType.Concat;
                        }
                        NextChar(); //SKip the third '.'.
                        return TokenType.Dots;
                    }
                    else if (next >= '0' && next <= '9')
                    {
                        ReadNumeral();
                        return TokenType.Number;
                    }
                    else
                    {
                        NextChar();
                        return MakeToken('.');
                    }
                }
                case >= '0' and <= '9': //number
                {
                    ReadNumeral();
                    return TokenType.Number;
                }
                case default(char):
                    return TokenType.Eos;
                default:
                    if (_currentCharType == TemplateTokenizer.TokenType.Text)
                    {
                        //Identifier or keyword.
                        var ret = ReadIdentifier();
                        if (_keywords.TryGetValue(ret, out var keyword))
                        {
                            return keyword;
                        }
                        return TokenType.Name;
                    }
                    else
                    {
                        //Single char token.
                        var retChar = _currentChar;
                        NextChar();
                        return MakeToken(retChar);
                    }
                }
            }
        }

        //Behavior:
        //If it's a valid sep, skip the sep.
        //If it's a "[" (or "]") (or an invalid sep), only skip the '[' (or ']').
        //Note that for invalid sep the caller should throw (except used as comments) so it does not matter.
        private int SkipSep()
        {
            var s = _currentChar;
            NextChar(); //Skip '[' or ']'.

            int count = 0;
            for (; PeekChar(count).c == '='; ++count)
            {
            }

            if (PeekChar(count).c == s)
            {
                for (int i = 0; i < count + 1; ++i)
                {
                    NextChar();
                }
                return count;
            }
            return -count - 1;
        }

        //Starting sep has been skipped (this is different from original Lua impl).
        private void ReadLongString(int sep)
        {
            while (true)
            {
                switch (_currentChar)
                {
                case default(char):
                    throw new Exception("Unfinished long string or comment");
                case ']':
                    if (SkipSep() == sep)
                    {
                        //Success.
                        return;
                    }
                    break;
                default:
                    NextChar();
                    break;
                }
            }
        }

        private void ReadString()
        {
            var s = _currentChar;
            NextChar();
            while (_currentChar != s)
            {
                if (_currentChar == default)
                {
                    throw new Exception("Invalid string literal");
                }
                if (_currentChar == '\\')
                {
                    if (PeekChar(1).c == s)
                    {
                        NextChar();
                        NextChar();
                        continue;
                    }
                }
                NextChar();
            }
            NextChar(); //Skip the end '"' or '\''.
        }

        private void ReadNumeral()
        {
            var nextChar = PeekChar(1).c;
            var useBinaryExp = _currentChar == '0' && nextChar == 'x' || nextChar == 'X';
            if (useBinaryExp)
            {
                NextChar();
                NextChar();
            }
            bool IsNumeralChar(char c)
            {
                //Lua allows decimal point in current culture. We don't allow it here for simplicity.
                return c >= '0' && c <= '9' ||
                    c == '+' || c == '-' || c == '.' ||
                    char.ToLower(c) == (useBinaryExp ? 'p' : 'e');
            }
            while (IsNumeralChar(NextChar()))
            {
            }
        }

        private string ReadIdentifier()
        {
            _identifierBuilder.Clear();
            CopyTextTokens(_identifierBuilder);
            return _identifierBuilder.ToString();
        }
    }
}
