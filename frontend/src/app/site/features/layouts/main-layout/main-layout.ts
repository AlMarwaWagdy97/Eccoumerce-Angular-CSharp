import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Navbar } from "../navbar/navbar";
import { FooterComponent } from "../footer/footer";

@Component({
  selector: 'app-main-layout',
  imports: [RouterOutlet, Navbar, FooterComponent],
  templateUrl: './main-layout.html',
  styleUrl: './main-layout.scss',
})
export class MainLayoutComponent {}
