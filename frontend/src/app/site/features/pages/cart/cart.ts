import { Component, inject } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CartServices } from '../../../core/services/cart-services';

@Component({
  selector: 'app-cart',
  imports: [RouterLink, CurrencyPipe],
  templateUrl: './cart.html',
  styleUrl: './cart.scss',
})
export class CartComponent {
  protected cart = inject(CartServices);
}
