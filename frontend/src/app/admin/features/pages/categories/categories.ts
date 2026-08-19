import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CategoryServices } from '../../../core/services/category-services';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';
import { AdminCategoryInterface, CategoryTreeRow } from '../../../shared/interface/categoryInterface';
import { Environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-admin-categories',
  imports: [ReactiveFormsModule],
  templateUrl: './categories.html',
  styleUrl: './categories.scss',
})
export class Categories {
  private categoryService = inject(CategoryServices);
  private auth = inject(AdminAuthServices);
  private fb = inject(FormBuilder);

  // Uploaded images are served by the API host, not the Angular dev server.
  private readonly assetOrigin = Environment.apiUrl.replace(/\/api\/?$/, '');

  categories = signal<AdminCategoryInterface[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal('');
  showForm = signal(false);
  editingId = signal<number | null>(null);
  busyId = signal<number | null>(null);

  treeView = signal(false);
  expandedIds = signal<Set<number>>(new Set());

  selectedFile = signal<File | null>(null);
  existingImage = signal<string | null>(null);

  canManage = () => this.auth.hasPermission('categories.manage');

  form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    slug: ['', Validators.required],
    parentId: [0],
    description: [''],
    sort: [0],
    metaDescription: [''],
    metaKey: [''],
    feature: [false],
    status: [true],
  });

  // Only top-level categories can be picked as a parent, and a category can
  // never be its own parent (the backend rejects that with Category.InvalidParent).
  parentOptions = computed(() =>
    this.categories().filter(c => !c.parentId && c.id !== this.editingId())
  );

  treeRows = computed<CategoryTreeRow[]>(() => {
    const byParent = new Map<number | null, AdminCategoryInterface[]>();
    for (const category of this.categories()) {
      const key = category.parentId ?? null;
      const siblings = byParent.get(key) ?? [];
      siblings.push(category);
      byParent.set(key, siblings);
    }

    const expanded = this.expandedIds();
    const rows: CategoryTreeRow[] = [];

    const walk = (parentId: number | null, depth: number): void => {
      for (const category of byParent.get(parentId) ?? []) {
        const children = byParent.get(category.id) ?? [];
        const isExpanded = expanded.has(category.id);
        rows.push({ category, depth, hasChildren: children.length > 0, expanded: isExpanded });
        if (isExpanded) walk(category.id, depth + 1);
      }
    };

    walk(null, 0);
    return rows;
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.categoryService.getCategories().subscribe({
      next: data => {
        this.categories.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  imageUrl(path?: string | null): string {
    if (!path) return '';
    return /^https?:\/\//i.test(path) ? path : `${this.assetOrigin}${path}`;
  }

  parentTitle(category: AdminCategoryInterface): string {
    if (!category.parentId) return '—';
    return this.categories().find(c => c.id === category.parentId)?.title ?? '—';
  }

  indent(depth: number): string {
    return `${depth * 1.5}rem`;
  }

  toggleTreeView(): void {
    this.treeView.update(v => !v);
  }

  toggleExpanded(id: number): void {
    this.expandedIds.update(ids => {
      const next = new Set(ids);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  startAdd(): void {
    this.editingId.set(null);
    this.selectedFile.set(null);
    this.existingImage.set(null);
    this.form.reset({ title: '', slug: '', parentId: 0, description: '', sort: 0, metaDescription: '', metaKey: '', feature: false, status: true });
    this.showForm.set(true);
  }

  startEdit(category: AdminCategoryInterface): void {
    this.editingId.set(category.id);
    this.selectedFile.set(null);
    this.existingImage.set(category.image ?? null);
    this.form.reset({
      title: category.title,
      slug: category.slug,
      parentId: category.parentId ?? 0,
      description: category.description ?? '',
      sort: category.sort ?? 0,
      metaDescription: category.metaDescription ?? '',
      metaKey: category.metaKey ?? '',
      feature: category.feature,
      status: category.status,
    });
    this.showForm.set(true);
  }

  cancel(): void {
    this.showForm.set(false);
    this.error.set('');
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
  }

  private buildFormData(): FormData {
    const raw = this.form.getRawValue();
    const payload = new FormData();

    payload.append('Title', raw.title);
    payload.append('Slug', raw.slug);
    payload.append('Feature', String(raw.feature));
    payload.append('Status', String(raw.status));
    payload.append('Sort', String(raw.sort ?? 0));

    // A <select> always yields a string, so "0" (the "None" option) is truthy —
    // coerce before testing, or a top-level category would post ParentId=0 and
    // blow up on the FK.
    const parentId = Number(raw.parentId);
    if (parentId > 0) payload.append('ParentId', String(parentId));

    if (raw.description) payload.append('Description', raw.description);
    if (raw.metaDescription) payload.append('MetaDescription', raw.metaDescription);
    if (raw.metaKey) payload.append('MetaKey', raw.metaKey);

    const file = this.selectedFile();
    if (file) {
      payload.append('ImageFile', file, file.name);
    } else if (this.existingImage()) {
      // Sending the current path back is how "leave the image alone" is expressed.
      payload.append('Image', this.existingImage()!);
    }

    return payload;
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set('');

    const editingId = this.editingId();
    const payload = this.buildFormData();
    const request$ = editingId
      ? this.categoryService.updateCategory(editingId, payload)
      : this.categoryService.createCategory(payload);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not save this category. Check the slug is unique and the image is a JPG/PNG/WebP under 2 MB.');
      },
    });
  }

  toggleStatus(category: AdminCategoryInterface): void {
    this.busyId.set(category.id);
    this.categoryService.toggleStatus(category.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.load();
      },
      error: () => this.busyId.set(null),
    });
  }

  remove(category: AdminCategoryInterface): void {
    this.busyId.set(category.id);
    this.categoryService.deleteCategory(category.id).subscribe({
      next: () => {
        this.categories.update(items => items.filter(c => c.id !== category.id));
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }
}
