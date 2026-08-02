import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Navbar } from "../navbar/navbar";
import { FooterComponent } from "../footer/footer";

@Component({
  selector: 'app-not-found',
  imports: [Navbar, FooterComponent, RouterLink],
  templateUrl: './not-found.html',
  styleUrl: './not-found.scss',
})
export class NotFoundComponent {}
