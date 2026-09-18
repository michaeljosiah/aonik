using Aonik.SharedKernel.Modules;

// Spec 097 §5 — assembly-level module identity. Everything that needs to know which module a type
// belongs to (the HTTP gate, agent descriptors, job definitions, event handlers) resolves it from
// Type.Assembly through ModuleCatalog.TryGetModuleId. Declared once, here, per module assembly.
[assembly: AonikModule(ModuleIds.Documents)]
