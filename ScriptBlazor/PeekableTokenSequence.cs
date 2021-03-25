using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor
{
    public sealed class PeekableTokenSequence : ITokenSequence
    {
        private readonly ITokenSequence _tokenizer;
        private bool _tokenizerEOS = false;
        private readonly List<(TemplateTokenizer.TokenType type, char[] content)> _backwardBuffer = new();
        private int? _peekLimit = null;

        public PeekableTokenSequence(ITokenSequence tokenizer)
        {
            _tokenizer = tokenizer;
        }

        public TemplateTokenizer.Token Current
        {
            get
            {
                if (_backwardBuffer.Count > 0)
                {
                    return new() { Type = _backwardBuffer[0].type, Content = _backwardBuffer[0].content };
                }
                return _tokenizer.Current;
            }
        }

        public bool MoveNext()
        {
            if (_peekLimit == 1)
            {
                return false;
            }
            if (_backwardBuffer.Count > 0)
            {
                _backwardBuffer.RemoveAt(0);
                _peekLimit -= 1;
                return true;
            }
            return !_tokenizerEOS && _tokenizer.MoveNext();
        }

        //Set a limit on peek. This will limit the code parsing in attribute
        //to not go outside the attribute.
        public void SetPeekLimit(int peekCount)
        {
            if (_peekLimit.HasValue)
            {
                throw new InvalidOperationException();
            }
            if (peekCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(peekCount));
            }
            _peekLimit = peekCount;
        }

        public void ClearPeekLimit()
        {
            if (!_peekLimit.HasValue)
            {
                throw new InvalidOperationException();
            }
            _peekLimit = null;
        }

        //Try to peek the token at the specified relative position.
        public bool TryPeek(int pos, out TemplateTokenizer.Token value)
        {
            if (pos >= _peekLimit)
            {
                value = default;
                return false;
            }
            if (pos < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pos));
            }
            while (pos > _backwardBuffer.Count && MoveNextToBuffer())
            {
            }
            if (pos < _backwardBuffer.Count)
            {
                value = new TemplateTokenizer.Token
                {
                    Type = _backwardBuffer[pos].type,
                    Content = _backwardBuffer[pos].content,
                };
                return true;
            }
            else if (pos == _backwardBuffer.Count)
            {
                value = _tokenizer.Current;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        //Move one token from input into buffer.
        //This method may consume the last token from the input and set _tokenizerEOS.
        //Return whether the input still can be read (i.e. the value of _tokenizerEOS).
        private bool MoveNextToBuffer()
        {
            if (_tokenizerEOS) return false;

            var t = _tokenizer.Current;
            _backwardBuffer.Add((t.Type, t.Content.ToArray()));
            return !(_tokenizerEOS = !_tokenizer.MoveNext());
        }

        //Split the current token into 2.
        public void SplitCurrent(int position)
        {
            Split(0, position);
        }

        public void Split(int peekCount, int position)
        {
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            if (peekCount >= _peekLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(peekCount));
            }
            if (_backwardBuffer.Count < peekCount)
            {
                //The requested token is not in _backwardBuffer or Current.
                //How do you know where to split?
                throw new ArgumentOutOfRangeException(nameof(peekCount));
            }
            if (_backwardBuffer.Count == peekCount)
            {
                if (_tokenizerEOS)
                {
                    //No more token to split.
                    throw new InvalidOperationException();
                }
                MoveNextToBuffer();
            }

            var (type, content) = _backwardBuffer[peekCount];
            if (position == 0)
            {
                return;
            }
            if (position > content.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            var firstSegment = content[..position];
            var secondSegment = position < content.Length ? content[position..] : null;
            _backwardBuffer[peekCount] = (type, firstSegment);
            if (secondSegment is not null)
            {
                _backwardBuffer.Insert(peekCount + 1, (type, secondSegment));
                _peekLimit += 1;
            }
        }
    }
}
