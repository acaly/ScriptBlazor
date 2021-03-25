using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor
{
    public interface ITokenSequence
    {
        bool MoveNext();
        TemplateTokenizer.Token Current { get; }
    }

    public static class TokenSequenceExtensions
    {
        public static void EnsureMoveNext(this ITokenSequence tok)
        {
            if (!tok.MoveNext())
            {
                //This is an internal exception.
                throw new Exception();
            }
        }
    }
}
