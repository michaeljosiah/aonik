import type { PanelManifest } from './types';

/**
 * In-memory registry of panel manifests.
 *
 * Panels declare the context keys they emit/consume and the workspace
 * actions they may request, enabling discovery and auto-wiring.
 */
export class PanelRegistry {
  private panels = new Map<string, PanelManifest>();

  /** Register (or overwrite) a panel manifest. */
  registerPanel(manifest: PanelManifest): void {
    this.panels.set(manifest.panelId, manifest);
  }

  /** Remove a panel from the registry. */
  unregisterPanel(panelId: string): void {
    this.panels.delete(panelId);
  }

  /** Look up a single panel manifest by ID. */
  getPanel(panelId: string): PanelManifest | undefined {
    return this.panels.get(panelId);
  }

  /** Check whether a panel is registered. */
  hasPanel(panelId: string): boolean {
    return this.panels.has(panelId);
  }

  /** Return all registered panel manifests. */
  getPanels(): PanelManifest[] {
    return Array.from(this.panels.values());
  }

  /** Find panels that emit a given context key. */
  findByEmittedContext(contextKey: string): PanelManifest[] {
    return this.getPanels().filter(
      (p) => p.emitsContext?.includes(contextKey) ?? false,
    );
  }

  /** Find panels that consume a given context key. */
  findByConsumedContext(contextKey: string): PanelManifest[] {
    return this.getPanels().filter(
      (p) => p.consumesContext?.includes(contextKey) ?? false,
    );
  }
}
