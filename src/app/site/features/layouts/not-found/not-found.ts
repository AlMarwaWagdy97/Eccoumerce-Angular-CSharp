import { Component } from '@angular/core';
import { Navbar } from "../navbar/navbar";
import { FooterComponent } from "../footer/footer";

@Component({
  selector: 'app-not-found',
  imports: [Navbar, FooterComponent],
  templateUrl: './not-found.html',
  styleUrl: './not-found.scss',
})
export class NotFoundComponent {}
