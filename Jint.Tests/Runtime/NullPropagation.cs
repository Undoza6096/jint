﻿using Esprima;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Jint.Runtime.References;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class NullPropagation
    {
        public class NullPropagationReferenceResolver : IReferenceResolver
        {
            public bool TryUnresolvableReference(Engine engine, Reference reference, out JsValue value)
            {
                value = reference.GetBase();
                return true;
            }

            public bool TryPropertyReference(Engine engine, Reference reference, ref JsValue value)
            {
                return value.IsNull() || value.IsUndefined();
            }

            public bool TryGetCallable(Engine engine, object callee, out JsValue value)
            {
                if (callee is Reference reference)
                {
                    var name = reference.GetReferencedName().AsString();
                    if (name == "filter")
                    {
                        value = new ClrFunctionInstance(engine, "map", (thisObj, values) => engine.Realm.Intrinsics.Array.ArrayCreate(0));
                        return true;
                    }
                }

                value = new ClrFunctionInstance(engine, "anonymous", (thisObj, values) => thisObj);
                return true;
            }

            public bool CheckCoercible(JsValue value)
            {
                return true;
            }
        }

        [Fact]
        public void CanCallFilterOnNull()
        {
            var engine = new Engine(cfg => cfg.SetReferencesResolver(new NullPropagationReferenceResolver()));

            const string Script = @"
var input = {};

var output = { Tags : input.Tags.filter(x=>x!=null) };
";

            engine.Execute(Script);

            var output = engine.GetValue("output").AsObject();

            Assert.True(output.Get("Tags").IsArray());
        }

        [Fact]
        public void NullPropagationTest()
        {
            var engine = new Engine(cfg => cfg.SetReferencesResolver(new NullPropagationReferenceResolver()));

            const string Script = @"
var input = {
	Address : null
};

var address = input.Address;
var city = input.Address.City;
var length = input.Address.City.length;

var output = {
	Count1 : input.Address.City.length,
	Count2 : this.XYZ.length
};
";

            engine.Execute(Script);

            var address = engine.GetValue("address");
            var city = engine.GetValue("city");
            var length = engine.GetValue("length");
            var output = engine.GetValue("output").AsObject();

            Assert.Equal(Null.Instance, address);
            Assert.Equal(Null.Instance, city);
            Assert.Equal(Null.Instance, length);

            Assert.Equal(Null.Instance, output.Get("Count1"));
            Assert.Equal(Undefined.Instance, output.Get("Count2"));
        }

        [Fact]
        public void NullPropagationFromArg()
        {
            var engine = new Engine(cfg => cfg.SetReferencesResolver(new NullPropagationReferenceResolver()));


            const string Script = @"
function test(arg) {
    return arg.Name;
}

function test2(arg) {
    return arg.Name.toUpperCase();
}
";
            engine.Execute(Script);
            var result = engine.Invoke("test", Null.Instance);

            Assert.Equal(Null.Instance, result);

            result = engine.Invoke("test2", Null.Instance);

            Assert.Equal(Null.Instance, result);
        }

        [Fact]
        public void NullPropagationShouldNotAffectOperators()
        {
            var engine = new Engine(cfg => cfg.SetReferencesResolver(new NullPropagationReferenceResolver()));

            var jsObject = engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);
            jsObject.Set("NullField", JsValue.Null);

            var script = @"
this.is_nullfield_not_null = this.NullField !== null;
this.is_notnullfield_not_null = this.NotNullField !== null;
this.has_emptyfield_not_null = this.EmptyField !== null;
";

            var wrapperScript = string.Format(@"function ExecutePatchScript(docInner){{ (function(doc){{ {0} }}).apply(docInner); }};", script);

            engine.Execute(wrapperScript, new ParserOptions("main.js"));

            engine.Invoke("ExecutePatchScript", jsObject);

            Assert.False(jsObject.Get("is_nullfield_not_null").AsBoolean());
            Assert.True(jsObject.Get("is_notnullfield_not_null").AsBoolean());
            Assert.True(jsObject.Get("has_emptyfield_not_null").AsBoolean());
        }
    }
}