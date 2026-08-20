import { Pipe, PipeTransform } from '@angular/core';
import { Environment } from '../../../../environments/environment';

// Uploaded images (categories, products) are stored as paths relative to the
// API host (e.g. "/uploads/categories/x.jpg"), not the Angular dev/SSR
// server that's actually serving this page — resolve against the API origin.
// Already-absolute URLs (external placeholders) and empty paths pass through.
@Pipe({ name: 'resolveImageUrl' })
export class ResolveImageUrlPipe implements PipeTransform {
  private static readonly assetOrigin = Environment.apiUrl.replace(/\/api\/?$/, '');

  transform(path?: string | null): string {
    if (!path) return '';
    return /^https?:\/\//i.test(path) ? path : `${ResolveImageUrlPipe.assetOrigin}${path}`;
  }
}
