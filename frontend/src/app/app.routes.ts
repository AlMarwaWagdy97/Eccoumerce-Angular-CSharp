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
import { AdminLayoutComponent } from './admin/features/layouts/main-layout/main-layout';
import { AdminAuthLayoutComponent } from './admin/features/layouts/auth-layout/auth-layout';
import { LoginComponent as AdminLoginComponent } from './admin/features/auth/login/login';
import { ForgotPasswordComponent as AdminForgotPasswordComponent } from './admin/features/auth/forgot-password/forgot-password';
import { ResetPasswordComponent as AdminResetPasswordComponent } from './admin/features/auth/reset-password/reset-password';
import { DashboardComponent } from './admin/features/pages/dashboard/dashboard';
import { Admins as AdminsComponent } from './admin/features/pages/admins/admins';
import { Categories as AdminCategoriesComponent } from './admin/features/pages/categories/categories';
import { Products as AdminProductsComponent } from './admin/features/pages/products/products';
import { ClientsComponent } from './admin/features/pages/clients/clients';
import { SlidersComponent } from './admin/features/pages/sliders/sliders';
import { Orders as AdminOrdersComponent } from './admin/features/pages/orders/orders';
import { RolesComponent } from './admin/features/pages/roles/roles';
import { adminAuthGuard } from './admin/core/guards/admin-auth-guard';
import { adminPermissionGuard } from './admin/core/guards/admin-permission-guard';

export const routes: Routes = [
    { path: 'auth', component: AuthLayoutComponent, title: 'Auth', children: [
        { path: 'login', component: LoginComponent, title: 'Login' },
        { path: 'register', component: RegisterComponent, title: 'Register' }
    ]},
    { path: 'admin/auth', component: AdminAuthLayoutComponent, title: 'Admin', children: [
        { path: 'login', component: AdminLoginComponent, title: 'Admin Login' },
        { path: 'forgot-password', component: AdminForgotPasswordComponent, title: 'Forgot Password' },
        { path: 'reset-password', component: AdminResetPasswordComponent, title: 'Reset Password' },
    ]},
    { path: 'admin', component: AdminLayoutComponent, canActivate: [adminAuthGuard], children: [
        { path: '', component: DashboardComponent, title: 'Admin Dashboard' },
        { path: 'categories', component: AdminCategoriesComponent, canActivate: [adminPermissionGuard('categories.view')], title: 'Categories' },
        { path: 'products', component: AdminProductsComponent, canActivate: [adminPermissionGuard('products.view')], title: 'Products' },
        { path: 'clients', component: ClientsComponent, canActivate: [adminPermissionGuard('clients.view')], title: 'Clients' },
        { path: 'sliders', component: SlidersComponent, canActivate: [adminPermissionGuard('sliders.view')], title: 'Sliders' },
        { path: 'orders', component: AdminOrdersComponent, canActivate: [adminPermissionGuard('orders.view')], title: 'Orders' },
        { path: 'roles', component: RolesComponent, canActivate: [adminPermissionGuard('roles.manage')], title: 'Roles' },
        { path: 'admins', component: AdminsComponent, canActivate: [adminPermissionGuard('admins.manage')], title: 'Admins' },
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
