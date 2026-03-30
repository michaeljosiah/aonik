import { useState, useCallback, useEffect, useRef } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Switch } from '@/components/ui/switch';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Route, Plus, Search, Pencil, Trash2, AlertCircle, Loader2 } from 'lucide-react';
import { routePolicyService, aiModelService } from '@/services/aiService';
import type { RoutePolicyResponse, CreateRoutePolicyRequest, UpdateRoutePolicyRequest, AiModelResponse } from '@/types/ai';

const getErrorMessage = (err: unknown, fallback: string) => {
  if (err && typeof err === 'object' && 'userMessage' in err) {
    const message = String((err as { userMessage?: string }).userMessage ?? '').trim();
    if (message) return message;
  }
  return fallback;
};

const riskTierOptions = ['Low', 'Medium', 'High'];
const dataSensitivityOptions = ['Public', 'Internal', 'Confidential', 'Restricted'];

const riskTierColor = (tier: string) => {
  switch (tier.toLowerCase()) {
    case 'low': return 'bg-green-500/10 text-green-700 border-green-200';
    case 'medium': return 'bg-yellow-500/10 text-yellow-700 border-yellow-200';
    case 'high': return 'bg-red-500/10 text-red-700 border-red-200';
    default: return '';
  }
};

export function RoutePoliciesPage() {
  const [policies, setPolicies] = useState<RoutePolicyResponse[]>([]);
  const [models, setModels] = useState<AiModelResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const requestIdRef = useRef(0);

  // Dialog state
  const [showDialog, setShowDialog] = useState(false);
  const [editingPolicy, setEditingPolicy] = useState<RoutePolicyResponse | null>(null);
  const [saving, setSaving] = useState(false);

  // Form state
  const [formUseCase, setFormUseCase] = useState('');
  const [formRiskTier, setFormRiskTier] = useState('Low');
  const [formDataSensitivity, setFormDataSensitivity] = useState('Public');
  const [formCostCeiling, setFormCostCeiling] = useState(1000);
  const [formPrimaryModelId, setFormPrimaryModelId] = useState('');
  const [formFallbackModelIds, setFormFallbackModelIds] = useState('[]');
  const [formIsActive, setFormIsActive] = useState(true);

  const loadData = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);
    try {
      const [policiesResult, modelsResult] = await Promise.all([
        routePolicyService.list(searchQuery || undefined),
        aiModelService.list(),
      ]);
      if (requestIdRef.current !== requestId) return;
      setPolicies(policiesResult);
      setModels(modelsResult);
    } catch (err) {
      if (requestIdRef.current !== requestId) return;
      setError(getErrorMessage(err, 'Failed to load route policies'));
    } finally {
      if (requestIdRef.current === requestId) setLoading(false);
    }
  }, [searchQuery]);

  useEffect(() => { loadData(); }, [loadData]);

  const resetForm = () => {
    setFormUseCase('');
    setFormRiskTier('Low');
    setFormDataSensitivity('Public');
    setFormCostCeiling(1000);
    setFormPrimaryModelId('');
    setFormFallbackModelIds('[]');
    setFormIsActive(true);
  };

  const openCreate = () => {
    resetForm();
    setEditingPolicy(null);
    setShowDialog(true);
  };

  const openEdit = (policy: RoutePolicyResponse) => {
    setFormUseCase(policy.useCase);
    setFormRiskTier(policy.riskTier);
    setFormDataSensitivity(policy.dataSensitivity);
    setFormCostCeiling(policy.costCeiling);
    setFormPrimaryModelId(policy.primaryModelId);
    setFormFallbackModelIds(policy.fallbackModelIdsJson);
    setFormIsActive(policy.isActive);
    setEditingPolicy(policy);
    setShowDialog(true);
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      if (editingPolicy) {
        const request: UpdateRoutePolicyRequest = {
          riskTier: formRiskTier,
          dataSensitivity: formDataSensitivity,
          costCeiling: formCostCeiling,
          primaryModelId: formPrimaryModelId,
          fallbackModelIdsJson: formFallbackModelIds,
          isActive: formIsActive,
        };
        await routePolicyService.update(editingPolicy.id, request);
      } else {
        const request: CreateRoutePolicyRequest = {
          useCase: formUseCase,
          riskTier: formRiskTier,
          dataSensitivity: formDataSensitivity,
          costCeiling: formCostCeiling,
          primaryModelId: formPrimaryModelId,
          fallbackModelIdsJson: formFallbackModelIds || undefined,
          isActive: formIsActive,
        };
        await routePolicyService.create(request);
      }
      setShowDialog(false);
      loadData();
    } catch (err) {
      setError(getErrorMessage(err, 'Failed to save route policy'));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await routePolicyService.delete(id);
      loadData();
    } catch (err) {
      setError(getErrorMessage(err, 'Failed to delete route policy'));
    }
  };

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Route Policies</h1>
          <p className="text-muted-foreground text-sm mt-1">
            Configure which AI model is used for each use-case. Tenant-specific policies override global defaults.
          </p>
        </div>
        <Button onClick={openCreate}>
          <Plus className="mr-2 h-4 w-4" />
          Create Policy
        </Button>
      </div>

      <div className="relative max-w-sm">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <Input
          placeholder="Search by use case..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          className="pl-9"
        />
      </div>

      {error && (
        <div className="flex items-center gap-2 text-destructive text-sm">
          <AlertCircle className="h-4 w-4" />
          {error}
        </div>
      )}

      {loading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : policies.length === 0 ? (
        <div className="text-center py-12 text-muted-foreground">
          <Route className="h-12 w-12 mx-auto mb-4 opacity-30" />
          <p>No route policies found</p>
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {policies.map((policy) => (
            <Card key={policy.id} className="relative group">
              <CardHeader className="pb-3">
                <div className="flex items-start justify-between">
                  <div className="space-y-1">
                    <CardTitle className="text-base font-semibold font-mono">{policy.useCase}</CardTitle>
                    <div className="flex items-center gap-2 flex-wrap">
                      <Badge className={`text-xs ${riskTierColor(policy.riskTier)}`}>
                        {policy.riskTier}
                      </Badge>
                      {policy.isActive ? (
                        <Badge className="text-xs bg-green-500/10 text-green-700 border-green-200">Active</Badge>
                      ) : (
                        <Badge variant="secondary" className="text-xs">Inactive</Badge>
                      )}
                      {policy.isOverride && (
                        <Badge className="text-xs bg-blue-500/10 text-blue-700 border-blue-200">Override</Badge>
                      )}
                    </div>
                  </div>
                  <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                    <Button variant="ghost" size="icon-sm" onClick={() => openEdit(policy)}>
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button variant="ghost" size="icon-sm" onClick={() => handleDelete(policy.id)}>
                      <Trash2 className="h-3.5 w-3.5 text-destructive" />
                    </Button>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                <div className="text-sm space-y-1">
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Model</span>
                    <span className="font-medium">{policy.primaryModelName ?? 'Not set'}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Sensitivity</span>
                    <span>{policy.dataSensitivity}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Cost Ceiling</span>
                    <span>${policy.costCeiling.toFixed(2)}</span>
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Dialog open={showDialog} onOpenChange={setShowDialog}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>{editingPolicy ? `Edit: ${editingPolicy.useCase}` : 'Create Route Policy'}</DialogTitle>
            <DialogDescription>
              {editingPolicy ? 'Update the route policy configuration.' : 'Create a new AI model routing policy.'}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2 max-h-[60vh] overflow-y-auto">
            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="use-case">Use Case</label>
              <Input
                id="use-case"
                value={formUseCase}
                onChange={(e) => setFormUseCase(e.target.value)}
                placeholder="e.g. title-generation"
                disabled={!!editingPolicy}
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="primary-model">Primary Model</label>
              <Select value={formPrimaryModelId} onValueChange={setFormPrimaryModelId}>
                <SelectTrigger id="primary-model">
                  <SelectValue placeholder="Select a model" />
                </SelectTrigger>
                <SelectContent>
                  {models.map((model) => (
                    <SelectItem key={model.id} value={model.id}>
                      {model.modelName} ({model.providerName})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="risk-tier">Risk Tier</label>
                <Select value={formRiskTier} onValueChange={setFormRiskTier}>
                  <SelectTrigger id="risk-tier">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {riskTierOptions.map((tier) => (
                      <SelectItem key={tier} value={tier}>{tier}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="data-sensitivity">Data Sensitivity</label>
                <Select value={formDataSensitivity} onValueChange={setFormDataSensitivity}>
                  <SelectTrigger id="data-sensitivity">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {dataSensitivityOptions.map((ds) => (
                      <SelectItem key={ds} value={ds}>{ds}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="cost-ceiling">Cost Ceiling ($)</label>
              <Input
                id="cost-ceiling"
                type="number"
                value={formCostCeiling}
                onChange={(e) => setFormCostCeiling(Number(e.target.value))}
                min={0}
                step={0.01}
              />
            </div>

            <div className="flex items-center gap-2">
              <Switch
                id="active"
                checked={formIsActive}
                onCheckedChange={setFormIsActive}
              />
              <label className="text-sm font-medium" htmlFor="active">Active</label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowDialog(false)}>Cancel</Button>
            <Button onClick={handleSave} disabled={saving || (!editingPolicy && !formUseCase)}>
              {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {editingPolicy ? 'Save Changes' : 'Create'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
