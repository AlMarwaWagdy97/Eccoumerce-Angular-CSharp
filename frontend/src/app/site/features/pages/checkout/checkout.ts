import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CartServices } from '../../../core/services/cart-services';

@Component({
  selector: 'app-checkout',
  imports: [RouterLink, ReactiveFormsModule, CurrencyPipe],
  templateUrl: './checkout.html',
  styleUrl: './checkout.scss',
})
export class CheckoutComponent {
  protected cart = inject(CartServices);
  private fb = inject(FormBuilder);

  placing = signal(false);
  orderPlaced = signal(false);

  form = this.fb.nonNullable.group({
    fullName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phone: ['', Validators.required],
    address: ['', Validators.required],
    city: ['', Validators.required],
    postalCode: ['', Validators.required],
    country: ['', Validators.required],
    paymentMethod: ['cod', Validators.required],
  });

  placeOrder(): void {
    if (this.form.invalid || this.cart.items().length === 0) {
      this.form.markAllAsTouched();
      return;
    }

    this.placing.set(true);

    // TODO: POST the order (this.form.value + this.cart.items()) to the backend
    // orders endpoint once it exists. For now we simulate a successful order.
    this.cart.clear();
    this.placing.set(false);
    this.orderPlaced.set(true);
  }

  invalid(controlName: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.invalid && control.touched;
  }
}
