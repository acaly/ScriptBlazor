using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor
{
    //Split the document into tokens. Here a token is one of the followings:
    //- whitespaces
    //- consecutive symbols (not including underscore)
    //- consecutive other characters (letters, etc.)
    public class TemplateTokenizer : ITokenSequence
    {
        public enum TokenType
        {
            //Never generated as output. Internally used to indicate non-initialized
            //state.
            Invalid = 0,

            Whitespace = 1,
            Symbols = 2,
            Text = 3,

            //Generated as the last token. IEnumerable model does not support checking
            //whether the sequence ends (without modifying its state), so we have to add
            //a special token for that.
            End = 4,
        }

        public readonly ref struct Token
        {
            public TokenType Type { get; init; }
            public ReadOnlySpan<char> Content { get; init; }

            public void Deconstruct(out TokenType type, out ReadOnlySpan<char> content)
            {
                type = Type;
                content = Content;
            }
        }

        private readonly TextReader _input;
        private bool _eos = false;

        private TokenType? _currentType;
        private char[] _buffer = new char[100];
        private int _bufferLength = 0;

        private static readonly TokenType[] _cachedCharacterTokenType = Enumerable.Range(0, 128)
            .Select(i => GetTokenTypeForCharInit(i))
            .ToArray();

        public TemplateTokenizer(TextReader input)
        {
            _input = input;
            Read();
        }

        private static TokenType GetTokenTypeForCharInit(int c)
        {
            char cc = (char)c;
            if (char.IsWhiteSpace(cc) || char.IsSeparator(cc) || char.IsControl(cc))
            {
                return TokenType.Whitespace;
            }
            if (char.IsSymbol(cc) || char.IsPunctuation(cc))
            {
                if (cc == '_')
                {
                    return TokenType.Text;
                }
                return TokenType.Symbols;
            }
            return TokenType.Text;
        }

        private static TokenType GetTokenTypeForChar(int c)
        {
            if (c < 128)
            {
                return _cachedCharacterTokenType[c];
            }
            return TokenType.Text;
        }

        private void ExtendBuffer()
        {
            var newBuffer = new char[_buffer.Length * 2];
            Array.Copy(_buffer, newBuffer, _bufferLength);
            _buffer = newBuffer;
        }

        public Token Current
        {
            get
            {
                if (!_currentType.HasValue)
                {
                    throw new InvalidOperationException();
                }
                return new() { Type = _currentType.Value, Content = new(_buffer, 0, _bufferLength) };
            }
        }

        private int _lastChar = 0;

        private int ReadLast()
        {
            return _lastChar;
        }

        private int Read()
        {
            return _lastChar = _input.Read();
        }

        public bool MoveNext()
        {
            int ch;
            if (_eos || (ch = ReadLast()) < 0)
            {
                if (_currentType != TokenType.End)
                {
                    //First MoveNext() after EOS. Generate the EOS token.
                    _currentType = TokenType.End;
                    _bufferLength = 0;
                    return true;
                }
                return false;
            }

            _bufferLength = 0;
            _currentType = GetTokenTypeForChar((char)ch);
            do
            {
                if (_bufferLength == _buffer.Length)
                {
                    ExtendBuffer();
                }
                _buffer[_bufferLength++] = (char)ch;
                ch = Read();
            } while (ch >= 0 && GetTokenTypeForChar(ch) == _currentType);

            _eos = ch < 0;
            return true;
        }

        public TemplateTokenizer GetEnumerator() => this;
    }
}
