﻿using esper.defs;
using esper.data;
using esper.plugins;
using esper.resolution;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using esper.data.headers;
using esper.setup;

namespace esper.elements {
    public class MainRecord : Container, IMainRecord {
        internal readonly IRecordHeader header;
        internal List<Subrecord> _unexpectedSubrecords;

        internal readonly PluginFile _file;
        internal readonly long bodyOffset;
        internal byte[] decompressedData;

        public MainRecordDef mrDef => def as MainRecordDef;
        public override MainRecord record => this;
        public override PluginFile file => _file;
        internal PluginManager pluginManager => _file.session.pluginManager;
        public GroupRecord childGroup { get; set; }

        public FormId formId => FormId.FromSource(_file, fileFormId);
        public UInt32 fileFormId => header.formId;
        public UInt32 localFormId => fileFormId & 0xFFFFFF;
        public UInt32? globalFormId => formId.globalFormId;

        public UInt32 dataSize => compressed
                    ? (UInt32) decompressedData.Length
                    : header.dataSize;
        public bool compressed => this.GetRecordFlag("Compressed");

        public string editorId {
            get => this.GetValue("EDID");
            set => this.SetValue("EDID", value);
        }

        public string fullName {
            get => this.GetValue("FULL");
            set => this.SetValue("FULL", value);
        }

        public ReadOnlyCollection<Subrecord> unexpectedSubrecords {
            get => _unexpectedSubrecords.AsReadOnly();
        }

        public override ReadOnlyCollection<Element> elements {
            get {
                if (_internalElements == null)
                    mrDef.ReadElements(this, file.source);
                return internalElements.AsReadOnly();
            }
        }

        public MainRecord(Container container, ElementDef def) 
            : base(container, def) {}

        public override void Initialize() {
            mrDef.InitElement(this);
        }

        public MainRecord(Container container, ElementDef def, PluginFileSource source)
            : base(container, def) {
            _file = container.file;
            header = manager.headerManager.ReadRecordHeader(source);
            bodyOffset = source.stream.Position;
            if (sessionOptions.readAllSubrecords)
                mrDef.ReadElements(this, source);
            source.stream.Position = bodyOffset + header.dataSize;
        }

        public static MainRecord Read(
            Container container,
            PluginFileSource source,
            Signature signature
        ) {
            var sig = signature.ToString();
            var def = (ElementDef) container.manager.GetRecordDef(sig);
            var record = new MainRecord(container, def, source);
            return record;
        }

        internal bool Decompress(PluginFileSource source) {
            if (decompressedData == null)
                decompressedData = source.Decompress(header.dataSize);
            source.SetDecompressedStream(decompressedData);
            if (decompressedData != null) return true;
            return false;
        }

        public bool IsLocal() {
            var masterCount = (_file as IMasterManager).originalMasters.Count;
            return (fileFormId >> 24) >= masterCount;
        }

        public override bool SupportsSignature(string sig) {
            return mrDef.memberDefs.Any(d => d.HasSignature(sig));
        }

        internal override void ElementsReady() {
            base.ElementsReady();
            Initialize();
        }

        internal override Element ResolveIn(Container container) {
            foreach (var element in container.internalElements) {
                if (element is MainRecord rec && rec.formId == formId) {
                    if (rec.signature != signature)
                        throw new Exception("Signature mismatch.");
                    return rec;
                }
            }
            return null;
        }

        internal GroupRecord CreateChildGroup() {
            if (childGroup != null) return childGroup;
            var groupDef = container.def.childDefs.FirstOrDefault(def => {
                return def is GroupDef groupDef && groupDef.isChildGroup;
            }) as GroupDef;
            if (groupDef == null) 
                throw new Exception($"Cannot create child group for {signature}");
            var label = BitConverter.GetBytes(fileFormId);
            childGroup = new GroupRecord(null, groupDef, label) {
                container = container
            };
            var index = container.internalElements.IndexOf(this) + 1;
            container.internalElements.Insert(index, childGroup);
            return childGroup;
        }

        internal override Element CopyInto(Container container, CopyOptions options) {
            var rec = new MainRecord(container, def);
            CopyChildrenInto(rec, options);
            if (container is GroupRecord group)
                mrDef.UpdateContainedIn(group, rec);
            if ((options & CopyOptions.CopyChildGroups) > 0)
                childGroup.CopyInto(rec, options);
            return rec;
        }

        public override Element CopyTo(Element target, CopyOptions options) {
            if (target is PluginFile) return target.AssignCopy(this, options);
            if (target is GroupRecord && target.def.ChildDefSupported(def))
                return target.AssignCopy(this, options);
            throw new Exception($"Cannot copy records into ${target.def.displayName}");
        }
    }
}
