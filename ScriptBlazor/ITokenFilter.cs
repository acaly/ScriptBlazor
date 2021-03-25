using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor
{
    public enum TokenFilterAction
    {
        None,
        BeginScriptBlock,
        BeginScriptExpression,
    }

    public enum TokenFilterEnv
    {
        Text,
        AttributeValue,
    }

    public interface ITokenFilter
    {
        TokenFilterAction FilterToken(TokenFilterEnv env, PeekableTokenSequence tokenSequence);
    }
}
