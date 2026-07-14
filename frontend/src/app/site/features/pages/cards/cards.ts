import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AccountServices } from '../../../core/services/account-services';
import { CardInterface } from '../../../shared/interface/account-interfaces';

@Component({
  selector: 'app-cards',
  imports: [ReactiveFormsModule],
  templateUrl: './cards.html',
  styleUrl: './cards.scss',
})
export class CardsComponent {
  private accountService = inject(AccountServices);
  private fb = inject(FormBuilder);

  cards = signal<CardInterface[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal('');
  showForm = signal(false);
  busyId = signal<number | null>(null);

  readonly currentYear = new Date().getFullYear();

  form = this.fb.nonNullable.group({
    cardholderName: ['', Validators.required],
    brand: ['Visa', Validators.required],
    last4: ['', [Validators.required, Validators.pattern(/^\d{4}$/)]],
    expiryMonth: [1, [Validators.required, Validators.min(1), Validators.max(12)]],
    expiryYear: [this.currentYear, [Validators.required, Validators.min(this.currentYear)]],
    isDefault: [false],
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.accountService.getCards().subscribe({
      next: data => {
        this.cards.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  startAdd(): void {
    this.form.reset({ brand: 'Visa', last4: '', expiryMonth: 1, expiryYear: this.currentYear, isDefault: false });
    this.error.set('');
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

    this.accountService.addCard(this.form.getRawValue()).subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not save this card. Please check the expiry date and try again.');
      },
    });
  }

  remove(card: CardInterface): void {
    this.busyId.set(card.id);
    this.accountService.deleteCard(card.id).subscribe({
      next: () => {
        this.cards.update(items => items.filter(c => c.id !== card.id));
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }

  setDefault(card: CardInterface): void {
    this.busyId.set(card.id);
    this.accountService.setDefaultCard(card.id).subscribe({
      next: () => {
        this.load();
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }
}
