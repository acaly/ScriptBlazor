using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor.LuaBlazor
{
    public class CodeGeneratedEventArgs
    {
        public string Code { get; init; }
    }

    public class LuaBlazorCompiler
    {
        private readonly Script _script;
        public event EventHandler<CodeGeneratedEventArgs> CodeGenerated;

        public LuaBlazorCompiler(CoreModules modules = CoreModules.Preset_HardSandbox)
        {
            _script = new(modules)
            {
                Options =
                {
                    Stdin = Stream.Null,
                    Stdout = Stream.Null,
                    Stderr = Stream.Null,
                },
            };
        }

        public CompiledLuaComponent Compile(TextReader input)
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

            var luaCode = codeGen.ToString();
            if (CodeGenerated is not null)
            {
                CodeGenerated(this, new() { Code = luaCode });
            }

            //Allow running on Blazor wasm.
            return new(_script.DoString(luaCode).Function);
        }
    }
}
