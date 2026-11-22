import { useEffect, useState } from 'react';
import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import { roleService } from '@/services/roleService';
import type { AccessRoleSummary } from '@/types';

export function TenantRoleSelect({ value, onChange, label, disabled = false }: {
  value: string; onChange: (value: string) => void; label: string; disabled?: boolean;
}) {
  const [roles, setRoles] = useState<AccessRoleSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [unavailable, setUnavailable] = useState(false);

  useEffect(() => {
    let current = true;
    void roleService.list({ pageNumber: 1, pageSize: 100 }).then(result => { if (current) setRoles(result.items); })
      .catch(() => { if (current) setUnavailable(true); }).finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, []);

  if (unavailable) return <>
    <Input aria-label={`${label} ID`} disabled={disabled} value={value} onChange={event => onChange(event.target.value)} />
    <p className="text-muted-foreground">Role names could not be loaded. An access administrator can provide the reviewer role ID.</p>
  </>;

  return <NativeSelect aria-label={label} disabled={disabled || loading} value={value} onChange={event => onChange(event.target.value)}>
    <option value="">{loading ? 'Loading roles…' : 'Choose a reviewer role'}</option>
    {value && !roles.some(role => role.roleId === value) && <option value={value}>Current reviewer role</option>}
    {roles.map(role => <option key={role.roleId} value={role.roleId}>{role.name}</option>)}
  </NativeSelect>;
}
