import { Component } from '@angular/core';

@Component({
  selector: 'app-products',
  imports: [],
  templateUrl: './products.html',
  styleUrl: './products.scss',
})
export class ProductsComponent {
     filterCategories = [
        { name: 'Electronics', count: 24 },
        { name: 'Fashion', count: 56 },
        { name: 'Home & Living', count: 38 },
        { name: 'Sports', count: 42 },
        { name: 'Beauty', count: 31 },
        { name: 'Books', count: 67 }
      ];
}
