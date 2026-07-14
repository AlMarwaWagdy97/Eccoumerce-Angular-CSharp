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
import { SingleCategoryComponent } from './site/features/pages/single-category/single-category';
import { CartComponent } from './site/features/pages/cart/cart';
import { CheckoutComponent } from './site/features/pages/checkout/checkout';
import { NotFoundComponent } from './site/features/layouts/not-found/not-found';
import { AccountLayoutComponent } from './site/features/layouts/account-layout/account-layout';
import { ProfileComponent } from './site/features/pages/profile/profile';
import { OrdersComponent } from './site/features/pages/orders/orders';
import { FavoritesComponent } from './site/features/pages/favorites/favorites';
import { TrackingComponent } from './site/features/pages/tracking/tracking';
import { AddressComponent } from './site/features/pages/address/address';
import { CardsComponent } from './site/features/pages/cards/cards';
import { authGuard } from './site/core/guards/auth-guard';

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
        { path: 'categories/:id', component: SingleCategoryComponent, title: 'Category' },
        { path: 'products', component: ProductsComponent, title: 'Products' },
        { path: 'products/:slug', component: ProductDetailsComponent, title: 'Product Details' },
        { path: 'cart', component: CartComponent, title: 'Cart' },
        { path: 'checkout', component: CheckoutComponent, title: 'Checkout' },

        { path: 'account', component: AccountLayoutComponent, canActivate: [authGuard], children: [
            { path: '', component: ProfileComponent, title: 'My Account' },
            { path: 'orders', component: OrdersComponent, title: 'My Orders' },
            { path: 'orders/:orderNumber', component: TrackingComponent, title: 'Track Order' },
            { path: 'address', component: AddressComponent, title: 'My Addresses' },
            { path: 'cards', component: CardsComponent, title: 'My Cards' },
            { path: 'favorites', component: FavoritesComponent, title: 'My Favorites' },
        ]},

        // Legacy flat paths redirect into the new /account/** shell.
        { path: 'profile', redirectTo: 'account', pathMatch: 'full' },
        { path: 'orders', redirectTo: 'account/orders', pathMatch: 'full' },
        { path: 'orders/:orderNumber/tracking', redirectTo: 'account/orders/:orderNumber' },
        { path: 'favorites', redirectTo: 'account/favorites', pathMatch: 'full' },
    ]},
    { path: '**', component: NotFoundComponent, title: 'Not Found' },

]
