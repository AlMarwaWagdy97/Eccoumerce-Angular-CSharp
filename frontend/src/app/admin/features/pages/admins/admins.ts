import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminUserServices } from '../../../core/services/admin-user-services';
import { RoleServices } from '../../../core/services/role-services';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';
import { AdminUserInterface } from '../../../shared/interface/admin-user-interfaces';
import { RoleInterface } from '../../../shared/interface/role-interfaces';

@Component({
  selector: 'app-admins',
  imports: [ReactiveFormsModule],
  templateUrl: './admins.html',
  styleUrl: './admins.scss',
})
export class Admins {
  private adminUserService = inject(AdminUserServices);
  private roleService = inject(RoleServices);
  private auth = inject(AdminAuthServices);

  private fb = inject(FormBuilder);

  admins = signal<AdminUserInterface[]>([]);
  roles = signal<RoleInterface[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal('');
  showForm = signal(false);
  editingId = signal<number | null>(null);
  busyId = signal<number | null>(null);

  currentAdminId = () => this.auth.user()?.id;

  form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: [''],
    roleId: [0, Validators.required],
  });

  constructor() {
    this.roleService.getRoles().subscribe(roles => this.roles.set(roles));
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.adminUserService.getAdmins().subscribe({
      next: data => {
        this.admins.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  startAdd(): void {
    this.editingId.set(null);
    this.form.reset({ roleId: this.roles()[0]?.id ?? 0 });
    this.form.get('email')?.enable();
    this.showForm.set(true);
  }

  startEdit(admin: AdminUserInterface): void {
    this.editingId.set(admin.id);
    this.form.reset({
      firstName: admin.firstName,
      lastName: admin.lastName,
      email: admin.email,
      phoneNumber: admin.phoneNumber ?? '',
      roleId: admin.roleId,
    });
    this.form.get('email')?.disable(); // email is immutable after creation
    this.showForm.set(true);
  }

  cancel(): void {
    this.showForm.set(false);
    this.error.set('');
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set('');

    const raw = this.form.getRawValue();
    const editingId = this.editingId();

    const request$ = editingId
      ? this.adminUserService.updateAdmin(editingId, {
          firstName: raw.firstName,
          lastName: raw.lastName,
          phoneNumber: raw.phoneNumber || undefined,
          roleId: raw.roleId,
          isActive: this.admins().find(a => a.id === editingId)?.isActive ?? true,
        })
      : this.adminUserService.createAdmin({
          firstName: raw.firstName,
          lastName: raw.lastName,
          email: raw.email,
          phoneNumber: raw.phoneNumber || undefined,
          roleId: raw.roleId,
        });

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not save this admin. Check the email is unique and try again.');
      },
    });
  }

  toggleStatus(admin: AdminUserInterface): void {
    this.busyId.set(admin.id);
    this.adminUserService.setAdminStatus(admin.id, !admin.isActive).subscribe({
      next: () => {
        this.load();
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }

  remove(admin: AdminUserInterface): void {
    this.busyId.set(admin.id);
    this.adminUserService.deleteAdmin(admin.id).subscribe({
      next: () => {
        this.admins.update(items => items.filter(a => a.id !== admin.id));
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }
}
