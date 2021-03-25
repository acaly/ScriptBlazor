using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor.LuaBlazor
{
    public sealed class LuaComponent : ComponentBase
    {
        private CompiledLuaComponent _luaComponentObj;
        private Table _luaComponentInstance;

        [Parameter]
        public CompiledLuaComponent LuaComponentObj
        {
            get => _luaComponentObj;
            set
            {
                if (_luaComponentObj != value)
                {
                    _luaComponentObj = value;
                    _luaComponentInstance = _luaComponentObj?.CreateComponentInstance();
                }
            }
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            InvokeLuaMethod("build", CreateBuilderTable(builder));
        }

        private void InvokeLuaMethod(string name, object parameters)
        {
            var table = _luaComponentInstance;
            table.Get(name).Function.Call(table, parameters);
        }

        private object CreateBuilderTable(RenderTreeBuilder builder)
        {
            return new Dictionary<string, object>
            {
                ["AddContent"] = (Action<int, object>)((sequence, val) => {
                    builder.AddContent(sequence, val.ToString());
                }),
                ["AddMarkupContent"] = (Action<int, object>)((sequence, val) => {
                    builder.AddMarkupContent(sequence, val.ToString());
                }),
                ["OpenElement"] = (Action<int, string>)((sequence, elementName) => {
                    builder.OpenElement(sequence, elementName);
                }),
                ["CloseElement"] = (Action)(() => {
                    builder.CloseElement();
                }),
                ["OpenComponent"] = (Action<int, string>)((sequence, typeName) => {
                    throw new NotImplementedException();
                }),
                ["CloseComponent"] = (Action)(() => {
                    builder.CloseComponent();
                }),
                ["AddAttribute"] = (Action<int, string, object>)((sequence, attribute, value) => {
                    if (value is null)
                    {
                        builder.AddAttribute(sequence, attribute);
                    }
                    else if (value is Closure closure)
                    {
                        builder.AddAttribute(sequence, attribute,
                            EventCallback.Factory.Create<EventArgs>(this, () => closure.Call()));
                    }
                    else
                    {
                        builder.AddAttribute(sequence, attribute, value.ToString());
                    }
                }),
                ["OpenRegion"] = (Action<int>)((sequence) => {
                    builder.OpenRegion(sequence);
                }),
                ["CloseRegion"] = (Action)(() => {
                    builder.CloseRegion();
                }),
            };
        }
    }
}
