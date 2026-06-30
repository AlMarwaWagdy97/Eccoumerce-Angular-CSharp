import { Routes } from '@angular/router';
import { AuthLayoutComponent } from './site/features/layouts/auth-layout/auth-layout';
import { LoginComponent } from './site/features/auth/login/login';
import { RegisterComponent } from './site/features/auth/register/register';
import { MainLayoutComponent } from './site/features/layouts/main-layout/main-layout';
import { HomeComponent } from './site/features/pages/home/home';
import { AboutComponent } from './site/features/pages/about/about';
import { CategoriesComponent } from './site/features/pages/categories/categories';
import { ProductsComponent } from './site/features/pages/products/products';
import { ProductDetailsComponent } from './site/features/pages/product-details/product-details';
import { CartComponent } from './site/features/pages/cart/cart';
import { CheckoutComponent } from './site/features/pages/checkout/checkout';
import { NotFoundComponent } from './site/features/layouts/not-found/not-found';

export const routes: Routes = [
    { path: 'auth', component: AuthLayoutComponent, title: 'Auth', children: [
        { path: 'login', component: LoginComponent, title: 'Login' },
        { path: 'register', component: RegisterComponent, title: 'Register' }
    ]},  
    { path: '', component: MainLayoutComponent, title: '', children: [
        { path: '', redirectTo: 'home', pathMatch: 'full' },
        { path: 'home', component: HomeComponent, title: 'Home' },
        { path: 'about-us', component: AboutComponent, title: 'About Us' },
        { path: 'categories', component: CategoriesComponent, title: 'Categories' },
        { path: 'categories/:id', component: CategoriesComponent, title: 'Categories' },
        { path: 'products', component: ProductsComponent, title: 'Products' },
        { path: 'products-details', component: ProductDetailsComponent, title: 'ProductDetails' },
        { path: 'cart', component: CartComponent, title: 'Cart' },
        { path: 'checkout', component: CheckoutComponent, title: 'Checkout' },
    ]},
    { path: '**', component: NotFoundComponent, title: 'Not Found' },

]