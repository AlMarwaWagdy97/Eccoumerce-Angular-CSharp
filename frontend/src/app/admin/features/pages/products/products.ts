import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ProductServices } from '../../../core/services/product-services';
import { CategoryServices } from '../../../core/services/category-services';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';
import { AdminProductDetailInterface, AdminProductInterface } from '../../../shared/interface/productInterface';
import { AdminCategoryInterface } from '../../../shared/interface/categoryInterface';
import { Environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-admin-products',
  imports: [ReactiveFormsModule, CurrencyPipe],
  templateUrl: './products.html',
  styleUrl: './products.scss',
})
export class Products {
  private productService = inject(ProductServices);
  private categoryService = inject(CategoryServices);
  private auth = inject(AdminAuthServices);
  private fb = inject(FormBuilder);

  private readonly pageSize = 20;
  // Uploaded images are served by the API host, not the Angular dev server.
  private readonly assetOrigin = Environment.apiUrl.replace(/\/api\/?$/, '');

  products = signal<AdminProductInterface[]>([]);
  categories = signal<AdminCategoryInterface[]>([]);
  page = signal(1);
  totalPages = signal(0);
  totalCount = signal(0);
  searchTerm = signal('');

  loading = signal(true);
  saving = signal(false);
  error = signal('');
  showForm = signal(false);
  editingId = signal<number | null>(null);
  busyId = signal<number | null>(null);

  selectedFile = signal<File | null>(null);
  existingImage = signal<string | null>(null);

  editingDetail = signal<AdminProductDetailInterface | null>(null);
  galleryFiles = signal<File[]>([]);
  uploadingGallery = signal(false);
  deletingImageId = signal<number | null>(null);

  canManage = () => this.auth.hasPermission('products.manage');

  form = this.fb.nonNullable.group({
    categoryId: [0, Validators.required],
    title: ['', Validators.required],
    slug: ['', Validators.required],
    sku: ['', Validators.required],
    price: [0, [Validators.required, Validators.min(0.01)]],
    priceAfterSale: [0],
    sale: [0],
    stockQuantity: [0],
    sort: [0],
    description: [''],
    metaDescription: [''],
    metaKey: [''],
    status: [true],
    feature: [false],
  });

  constructor() {
    this.load();
    this.categoryService.getCategories().subscribe(categories => this.categories.set(categories));
  }

  private load(): void {
    this.loading.set(true);
    this.productService.getProducts(this.searchTerm(), this.page(), this.pageSize).subscribe({
      next: data => {
        this.products.set(data.items);
        this.totalPages.set(data.totalPages);
        this.totalCount.set(data.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  categoryTitle(categoryId: number): string {
    return this.categories().find(c => c.id === categoryId)?.title ?? '—';
  }

  imageUrl(path?: string | null): string {
    if (!path) return '';
    return /^https?:\/\//i.test(path) ? path : `${this.assetOrigin}${path}`;
  }

  onSearchInput(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
  }

  search(): void {
    this.page.set(1);
    this.load();
  }

  goToPage(page: number): void {
    if (page < 1 || (this.totalPages() > 0 && page > this.totalPages())) return;
    this.page.set(page);
    this.load();
  }

  startAdd(): void {
    this.editingId.set(null);
    this.editingDetail.set(null);
    this.selectedFile.set(null);
    this.existingImage.set(null);
    this.galleryFiles.set([]);
    this.form.reset({
      categoryId: this.categories()[0]?.id ?? 0,
      title: '',
      slug: '',
      sku: '',
      price: 0,
      priceAfterSale: 0,
      sale: 0,
      stockQuantity: 0,
      sort: 0,
      description: '',
      metaDescription: '',
      metaKey: '',
      status: true,
      feature: false,
    });
    this.showForm.set(true);
  }

  startEdit(product: AdminProductInterface): void {
    this.editingId.set(product.id);
    this.editingDetail.set(null);
    this.selectedFile.set(null);
    this.existingImage.set(product.image ?? null);
    this.galleryFiles.set([]);
    this.form.reset({
      categoryId: product.categoryId,
      title: product.title,
      slug: product.slug,
      sku: product.sku,
      price: product.price,
      priceAfterSale: product.priceAfterSale ?? 0,
      sale: product.sale ?? 0,
      stockQuantity: product.stockQuantity,
      sort: product.sort ?? 0,
      description: '',
      metaDescription: product.metaDescription ?? '',
      metaKey: product.metaKey ?? '',
      status: product.status,
      feature: product.feature,
    });
    this.showForm.set(true);

    // The list response has no description or gallery — load the full
    // detail separately once the form is open.
    this.productService.getProduct(product.id).subscribe(detail => {
      this.editingDetail.set(detail);
      this.form.patchValue({ description: detail.description ?? '' });
    });
  }

  cancel(): void {
    this.showForm.set(false);
    this.error.set('');
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
  }

  onGalleryFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.galleryFiles.set(input.files ? Array.from(input.files) : []);
  }

  private buildFormData(): FormData {
    const raw = this.form.getRawValue();
    const payload = new FormData();

    payload.append('CategoryId', String(raw.categoryId));
    payload.append('Title', raw.title);
    payload.append('Slug', raw.slug);
    payload.append('Sku', raw.sku);
    payload.append('Price', String(raw.price));
    payload.append('Status', String(raw.status));
    payload.append('Feature', String(raw.feature));
    payload.append('StockQuantity', String(raw.stockQuantity ?? 0));
    payload.append('Sort', String(raw.sort ?? 0));

    if (raw.priceAfterSale) payload.append('PriceAfterSale', String(raw.priceAfterSale));
    if (raw.sale) payload.append('Sale', String(raw.sale));
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
      ? this.productService.updateProduct(editingId, payload)
      : this.productService.createProduct(payload);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not save this product. Check the SKU and slug are unique and the image is a JPG/PNG/WebP under 2 MB.');
      },
    });
  }

  uploadGalleryImages(): void {
    const id = this.editingId();
    const files = this.galleryFiles();
    if (!id || files.length === 0) return;

    this.uploadingGallery.set(true);
    this.productService.addImages(id, files).subscribe({
      next: added => {
        this.editingDetail.update(detail => (detail ? { ...detail, images: [...detail.images, ...added] } : detail));
        this.galleryFiles.set([]);
        this.uploadingGallery.set(false);
      },
      error: () => this.uploadingGallery.set(false),
    });
  }

  removeGalleryImage(imageId: number): void {
    const id = this.editingId();
    if (!id) return;

    this.deletingImageId.set(imageId);
    this.productService.deleteImage(id, imageId).subscribe({
      next: () => {
        this.editingDetail.update(detail => (detail ? { ...detail, images: detail.images.filter(i => i.id !== imageId) } : detail));
        this.deletingImageId.set(null);
      },
      error: () => this.deletingImageId.set(null),
    });
  }

  toggleStatus(product: AdminProductInterface): void {
    this.busyId.set(product.id);
    this.productService.toggleStatus(product.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.load();
      },
      error: () => this.busyId.set(null),
    });
  }

  remove(product: AdminProductInterface): void {
    this.busyId.set(product.id);
    this.productService.deleteProduct(product.id).subscribe({
      next: () => {
        this.products.update(items => items.filter(p => p.id !== product.id));
        this.totalCount.update(count => count - 1);
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }
}
