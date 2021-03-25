using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor.LuaBlazor
{
    public sealed class CompiledLuaComponent
    {
        private readonly Closure _func;

        public CompiledLuaComponent(Closure func)
        {
            _func = func;
        }

        internal Table CreateComponentInstance()
        {
            return _func.Call().Table;
        }
    }
}
