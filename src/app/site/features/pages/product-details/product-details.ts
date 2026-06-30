import { Component } from '@angular/core';

@Component({
  selector: 'app-product-details',
  imports: [],
  templateUrl: './product-details.html',
  styleUrl: './product-details.scss',
})
export class ProductDetailsComponent {

  reviewsList = [
    { name: 'John Doe', date: '2024-03-01', rating: 5, comment: 'Amazing sound quality! Best headphones I\'ve ever owned.' },
    { name: 'Jane Smith', date: '2024-03-05', rating: 4, comment: 'Great noise cancellation, comfortable for long use.' },
    { name: 'Mike Johnson', date: '2024-03-10', rating: 5, comment: 'Battery life is incredible. Highly recommend!' }
  ];
}
