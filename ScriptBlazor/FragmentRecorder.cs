using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor
{
    //Combine a series of instructions into a single IParsedTemplateObject.
    //TODO we should support merging static elements into a single AddMarkupContent call.
    public sealed class FragmentRecorder
    {
        private delegate void InstructionDelegate(ICodeGenerator c, int n, ref int s);

        private readonly List<InstructionDelegate> _list = new();

        private sealed class MergedObject : IParsedTemplateObject
        {
            public InstructionDelegate[] List { get; init; }

            public void WriteToOutput(ICodeGenerator writer, int nestLevel, ref int sequence)
            {
                foreach (var item in List)
                {
                    item(writer, nestLevel, ref sequence);
                }
            }
        }

        public void Write(IParsedTemplateObject obj)
        {
            _list.Add((ICodeGenerator g, int n, ref int s) => obj.WriteToOutput(g, n, ref s));
        }

        public void WriteMarkupContent(string str)
        {
            _list.Add((ICodeGenerator g, int n, ref int s) => g.WriteMarkupContent(n, s++, str));
        }

        public void WriteContent(IParsedExpressionObject expr)
        {
            _list.Add((ICodeGenerator g, int n, ref int s) => g.WriteContent(n, ref s, expr));
        }

        public void OpenElement(string elementName)
        {
            _list.Add((ICodeGenerator g, int n, ref int s) => g.OpenElement(n, s++, elementName));
        }

        public void CloseElement()
        {
            _list.Add((ICodeGenerator g, int n, ref int s) => g.CloseElement(n));
        }

        public void OpenComponent(IComponentTypeInfo componentType)
        {
            _list.Add((ICodeGenerator g, int n, ref int s) => g.OpenComponent(n, ref s, componentType));
        }

        public void CloseComponent()
        {
            _list.Add((ICodeGenerator g, int n, ref int s) => g.CloseComponent(n));
        }

        public void WriteChildObj(IParsedTemplateObject parsedCodeObj)
        {
            _list.Add((ICodeGenerator g, int n, ref int s) =>
            {
                g.OpenRegion(n, s++);
                parsedCodeObj.WriteToOutput(g, n, ref s);
                g.CloseRegion(n);
            });
        }

        public void WriteAttribute(string attributeName, IParsedExpressionObject parsedCodeObj)
        {
            _list.Add((ICodeGenerator g, int n, ref int s) => g.Attribute(n, ref s, attributeName, parsedCodeObj));
        }

        public IParsedTemplateObject ToParsedObject()
        {
            return new MergedObject
            {
                List = _list.ToArray(),
            };
        }
    }
}
