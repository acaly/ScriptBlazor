using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor
{
    public interface IFragmentParser
    {
        //template parser should try to fulfill its need first before asking script parser
        //for example, when inside a tag, tparser should match '>' as end of tag instead of asking sparser
        //similarly, when inside attr val, '"' should be parsed as end of attr val
        //but in text, '<' has low priority and should ask sparser whether it can be a code tag
        void Parse(PeekableTokenSequence input, ITokenFilter tokenFilter, ICodeParser codeParser, FragmentRecorder output);
    }
}
