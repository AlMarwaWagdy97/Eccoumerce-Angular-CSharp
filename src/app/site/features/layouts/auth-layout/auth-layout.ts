import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Navbar } from "../navbar/navbar";

@Component({
  selector: 'app-auth-layout',
  imports: [RouterOutlet, Navbar],
  templateUrl: './auth-layout.html',
  styleUrl: './auth-layout.scss',
})
export class AuthLayoutComponent {}
