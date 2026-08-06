import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RoleServices } from '../../../core/services/role-services';
import { PermissionInterface, RoleInterface } from '../../../shared/interface/role-interfaces';

@Component({
  selector: 'app-roles',
  imports: [ReactiveFormsModule],
  templateUrl: './roles.html',
  styleUrl: './roles.scss',
})
export class RolesComponent {
  private roleService = inject(RoleServices);
  private fb = inject(FormBuilder);

  roles = signal<RoleInterface[]>([]);
  permissionCatalog = signal<PermissionInterface[]>([]);
  selectedPermissionKeys = signal<Set<string>>(new Set());

  loading = signal(true);
  saving = signal(false);
  error = signal('');
  showForm = signal(false);
  editingId = signal<number | null>(null);
  busyId = signal<number | null>(null);

  permissionsByModule = computed(() => {
    const groups = new Map<string, PermissionInterface[]>();
    for (const permission of this.permissionCatalog()) {
      const group = groups.get(permission.module) ?? [];
      group.push(permission);
      groups.set(permission.module, group);
    }
    return Array.from(groups.entries()).map(([module, permissions]) => ({ module, permissions }));
  });

  form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    description: [''],
  });

  constructor() {
    this.roleService.getPermissionCatalog().subscribe(catalog => this.permissionCatalog.set(catalog));
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.roleService.getRoles().subscribe({
      next: data => {
        this.roles.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  startAdd(): void {
    this.editingId.set(null);
    this.form.reset();
    this.selectedPermissionKeys.set(new Set());
    this.showForm.set(true);
  }

  startEdit(role: RoleInterface): void {
    this.editingId.set(role.id);
    this.form.reset({ name: role.name, description: role.description ?? '' });
    this.selectedPermissionKeys.set(new Set(role.permissions.map(p => p.key)));
    this.showForm.set(true);
  }

  cancel(): void {
    this.showForm.set(false);
    this.error.set('');
  }

  isChecked(key: string): boolean {
    return this.selectedPermissionKeys().has(key);
  }

  togglePermission(key: string, checked: boolean): void {
    this.selectedPermissionKeys.update(keys => {
      const next = new Set(keys);
      checked ? next.add(key) : next.delete(key);
      return next;
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set('');

    const raw = this.form.getRawValue();
    const request = {
      name: raw.name,
      description: raw.description || undefined,
      permissionKeys: Array.from(this.selectedPermissionKeys()),
    };

    const editingId = this.editingId();
    const request$ = editingId ? this.roleService.updateRole(editingId, request) : this.roleService.createRole(request);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not save this role. Check the name is unique and try again.');
      },
    });
  }

  remove(role: RoleInterface): void {
    this.busyId.set(role.id);
    this.roleService.deleteRole(role.id).subscribe({
      next: () => {
        this.roles.update(items => items.filter(r => r.id !== role.id));
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }
}
