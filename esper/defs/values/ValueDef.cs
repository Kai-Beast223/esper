﻿using esper.elements;
using esper.parsing;
using esper.setup;
using Newtonsoft.Json.Linq;
using System;

namespace esper.defs {
    public class ValueDef : Def {
        public int size {
            get {
                if (!src.ContainsKey("size")) return 0;
                return src.Value<int>("size");
            }
        }
        public bool isVariableSize { get => size == 0; }

        public ValueDef(DefinitionManager manager, JObject src, Def parent)
            : base(manager, src, parent) {
        }

        public new Element ReadElement(Container container, PluginFileSource source) {
            return new ValueElement(container, this, source);
        }

        public new Element InitElement(Container container) {
            return new ValueElement(container, this);
        }

        public dynamic GetData(ValueElement element) {
            return element.data;
        }

        public void SetData(ValueElement element, dynamic data) {
            element.data = data;
            element.SetState(ElementState.Modified);
        }

        public string GetValue(ValueElement element) {
            throw new NotImplementedException();
        }

        public void SetValue(ValueElement element, string value) {
            throw new NotImplementedException();
        }

        public dynamic DefaultData() {
            throw new NotImplementedException();
        }

        public dynamic ReadData(PluginFileSource source) {
            throw new NotImplementedException();
        }
    }
}
