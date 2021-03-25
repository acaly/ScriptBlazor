using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor.LuaBlazor
{
    public static class LuaBlazorCompiler
    {
        public static CompiledLuaComponent Compile(TextReader input)
        {
            PeekableTokenSequence tok = new(new TemplateTokenizer(input));
            FragmentRecorder recorder = new();

            tok.MoveNext();
            while (tok.Current.Type != TemplateTokenizer.TokenType.End)
            {
                HtmlFragmentParser.Instance.Parse(tok, LuaCodeTokenFilter.Instance, LuaCodeParser.Instance, recorder);
            }

            var seq = 0;
            LuaBlazorCodeGenerator codeGen = new();
            recorder.ToParsedObject().WriteToOutput(codeGen, 0, ref seq);
            
            return new(Script.RunString(codeGen.ToString()).Function);
        }
    }
}
