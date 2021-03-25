# Script Blazor

This project adds the ability to write razor components with scripting languages. It supports both Blazor server and wasm.

***Note: This is a work in progress.*** There will be tons of bugs. I am trying to improve it recently.

TRY IT YOURSELF ==> https://acaly.github.io/ScriptBlazor/

## Why script?

There are mainly 2 reasons that drive me to write this library:

* Change razor component without recompiling and publishing the entire app.
* Execute razor component code in a sandboxed environment.

Combining these, it will be able to allow user of the website to upload highly customized UI elements for themselves.

## Design

Although Blazor is a new and complicated technology, a razor component itself is actually very simple.
This library compiles a code-mixed HTML file into a script that generates a function for the `BuildRenderTree`
method of the `ComponentBase` class (the base class of all razor components).

There is some basic abstraction in this library that theoretically allows one, given a proper implementation,
to write razor components in any programming language. However, being an extremely simple, fast, and safe language,
Lua is definitely the best option.

It depends on [MoonSharp](https://github.com/moonsharp-devs/moonsharp/) to run Lua on top of C# (even in wasm environment).

## How to use

The library provides a `LuaComponent` class, which accepts a parameter of `CompiledLuaComponent`, which can be generated
by `LuaBlazorCompiler.Compile` method.

There is an example on https://acaly.github.io/ScriptBlazor/ . The code showed initially is
```
@code
    local x = 0
    function self:test_func()
        return "Hello, Blazor!"
    end
    local function reset()
        x = 0
    end
end
<div style="height:250px;width:200px;border:solid 1px black;color:green;padding:20px">
    <div class="p-2">@self:test_func()</div>
    <div class="p-2">x = @x</div>
    <button class="px-3 m-2 btn btn-primary" @onclick="function() x = x + 1 end">+1</button>
    <button class="px-3 m-2 btn btn-primary" @onclick="function() x = x - 1 end">-1</button>
    @if (x % 2) == 0 then
        <button class="px-3 m-2 btn btn-warning" @onclick="reset">RESET</button>
    end
</div>
```
The Lua code generated from it is similar to (added indentation)
```lua
return function()
    local self = {}
    
    local x = 0
    function self:test_func()
        return "Hello, Blazor!"
    end
    local function reset()
        x = 0
    end
    
    function self:build(__builder0)
        __builder0.AddMarkupContent(0, "\n")
        __builder0.OpenElement(1, "div")
        __builder0.AddAttribute(2, "style", "height:250px;width:200px;border:solid 1px black;color:green;padding:20px")
        __builder0.AddMarkupContent(3, "\n    ")
        __builder0.OpenElement(4, "div")
        __builder0.AddAttribute(5, "class", "p-2")
        __builder0.AddContent(6, self:test_func())
        __builder0.CloseElement()
        __builder0.AddMarkupContent(7, "\n    ")
        __builder0.OpenElement(8, "div")
        __builder0.AddAttribute(9, "class", "p-2")
        __builder0.AddMarkupContent(10, "x")
        __builder0.AddMarkupContent(11, " ")
        __builder0.AddMarkupContent(12, "=")
        __builder0.AddMarkupContent(13, " ")
        __builder0.AddContent(14, x)
        __builder0.CloseElement()
        __builder0.AddMarkupContent(15, "\n    ")
        __builder0.OpenElement(16, "button")
        __builder0.AddAttribute(17, "class", "px-3 m-2 btn btn-primary")
        __builder0.AddAttribute(18, "onclick", function() x = x + 1 end)
        __builder0.AddMarkupContent(19, "+")
        __builder0.AddMarkupContent(20, "1")
        __builder0.CloseElement()
        __builder0.AddMarkupContent(21, "\n    ")
        __builder0.OpenElement(22, "button")
        __builder0.AddAttribute(23, "class", "px-3 m-2 btn btn-primary")
        __builder0.AddAttribute(24, "onclick", function() x = x - 1 end)
        __builder0.AddMarkupContent(25, "-")
        __builder0.AddMarkupContent(26, "1")
        __builder0.CloseElement()
        __builder0.AddMarkupContent(27, "\n    ")
        if (x % 2) == 0 then
            __builder0.OpenElement(28, "button")
            __builder0.AddAttribute(29, "class", "px-3 m-2 btn btn-warning")
            __builder0.AddAttribute(30, "onclick", reset)
            __builder0.AddMarkupContent(31, "RESET")
            __builder0.CloseElement()
            __builder0.AddMarkupContent(32, "\n")
        end
        __builder0.CloseElement()
    end
    return self
end
```
