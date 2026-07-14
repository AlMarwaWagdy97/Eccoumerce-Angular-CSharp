import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CartServices } from '../../../core/services/cart-services';
import { AccountServices } from '../../../core/services/account-services';

@Component({
  selector: 'app-checkout',
  imports: [RouterLink, ReactiveFormsModule, CurrencyPipe],
  templateUrl: './checkout.html',
  styleUrl: './checkout.scss',
})
export class CheckoutComponent {
  protected cart = inject(CartServices);
  protected account = inject(AccountServices);
  private fb = inject(FormBuilder);
  private router = inject(Router);

  placing = signal(false);
  orderPlaced = signal(false);
  placedOrderNumber = signal('');
  error = signal('');

  form = this.fb.nonNullable.group({
    fullName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phone: ['', Validators.required],
    address: ['', Validators.required],
    city: ['', Validators.required],
    state: ['', Validators.required],
    postalCode: ['', Validators.required],
    country: ['', Validators.required],
    paymentMethod: ['cod', Validators.required],
  });

  goToLogin(): void {
    this.router.navigate(['/auth/login'], { queryParams: { returnUrl: '/checkout' } });
  }

  placeOrder(): void {
    if (this.form.invalid || this.cart.items().length === 0) {
      this.form.markAllAsTouched();
      return;
    }

    if (!this.account.isLoggedIn()) {
      this.goToLogin();
      return;
    }

    this.placing.set(true);
    this.error.set('');

    const raw = this.form.getRawValue();

    this.account.addAddress({
      fullName: raw.fullName,
      phone: raw.phone,
      line1: raw.address,
      city: raw.city,
      state: raw.state,
      country: raw.country,
      postalCode: raw.postalCode,
      isDefault: false,
    }).subscribe({
      next: address => {
        this.account.createOrder({
          addressId: address.id,
          items: this.cart.items().map(item => ({ productId: item.id, quantity: item.quantity })),
        }).subscribe({
          next: order => {
            this.cart.clear();
            this.placing.set(false);
            this.placedOrderNumber.set(order.orderNumber);
            this.orderPlaced.set(true);
          },
          error: () => {
            this.placing.set(false);
            this.error.set('We could not place your order. Please check stock availability and try again.');
          },
        });
      },
      error: () => {
        this.placing.set(false);
        this.error.set('We could not save your shipping address. Please check the details and try again.');
      },
    });
  }

  invalid(controlName: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.invalid && control.touched;
  }
}
