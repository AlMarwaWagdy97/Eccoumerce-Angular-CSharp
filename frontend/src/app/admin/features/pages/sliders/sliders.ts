import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { SliderServices } from '../../../core/services/slider-services';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';
import { SliderInterface } from '../../../shared/interface/slider-interfaces';
import { Environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-admin-sliders',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './sliders.html',
  styleUrl: './sliders.scss',
})
export class SlidersComponent {
  private sliderService = inject(SliderServices);
  private auth = inject(AdminAuthServices);
  private fb = inject(FormBuilder);

  // Uploaded images are served by the API host, not the Angular dev server.
  private readonly assetOrigin = Environment.apiUrl.replace(/\/api\/?$/, '');

  sliders = signal<SliderInterface[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal('');
  showForm = signal(false);
  editingId = signal<number | null>(null);
  busyId = signal<number | null>(null);

  selectedFile = signal<File | null>(null);
  existingImage = signal<string | null>(null);

  canManage = () => this.auth.hasPermission('sliders.manage');

  form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    link: [''],
    sort: [0],
    status: [true],
    startsOn: [''],
    endsOn: [''],
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.sliderService.getSliders().subscribe({
      next: data => {
        this.sliders.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  imageUrl(path?: string | null): string {
    if (!path) return '';
    return /^https?:\/\//i.test(path) ? path : `${this.assetOrigin}${path}`;
  }

  // <input type="datetime-local"> wants "YYYY-MM-DDTHH:mm"; the API returns
  // a full ISO string, so trim it (and hand back '' for null).
  private toLocalInput(value?: string | null): string {
    return value ? value.slice(0, 16) : '';
  }

  startAdd(): void {
    this.editingId.set(null);
    this.selectedFile.set(null);
    this.existingImage.set(null);
    this.form.reset({ title: '', link: '', sort: 0, status: true, startsOn: '', endsOn: '' });
    this.showForm.set(true);
  }

  startEdit(slider: SliderInterface): void {
    this.editingId.set(slider.id);
    this.selectedFile.set(null);
    this.existingImage.set(slider.image ?? null);
    this.form.reset({
      title: slider.title,
      link: slider.link ?? '',
      sort: slider.sort ?? 0,
      status: slider.status,
      startsOn: this.toLocalInput(slider.startsOn),
      endsOn: this.toLocalInput(slider.endsOn),
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
    payload.append('Status', String(raw.status));
    payload.append('Sort', String(raw.sort ?? 0));

    if (raw.link) payload.append('Link', raw.link);
    if (raw.startsOn) payload.append('StartsOn', raw.startsOn);
    if (raw.endsOn) payload.append('EndsOn', raw.endsOn);

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

    if (!this.editingId() && !this.selectedFile()) {
      this.error.set('Pick an image — a new slider needs one.');
      return;
    }

    this.saving.set(true);
    this.error.set('');

    const editingId = this.editingId();
    const payload = this.buildFormData();
    const request$ = editingId
      ? this.sliderService.updateSlider(editingId, payload)
      : this.sliderService.createSlider(payload);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not save this slider. Check the end date is after the start date and the image is a JPG/PNG/WebP under 2 MB.');
      },
    });
  }

  toggleStatus(slider: SliderInterface): void {
    this.busyId.set(slider.id);
    this.sliderService.toggleStatus(slider.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.load();
      },
      error: () => this.busyId.set(null),
    });
  }

  remove(slider: SliderInterface): void {
    this.busyId.set(slider.id);
    this.sliderService.deleteSlider(slider.id).subscribe({
      next: () => {
        this.sliders.update(items => items.filter(s => s.id !== slider.id));
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }
}
