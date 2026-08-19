export interface AdminCategoryInterface {
  id: number;
  parentId?: number | null;
  title: string;
  slug: string;
  description?: string | null;
  image?: string | null;
  sort?: number | null;
  feature: boolean;
  status: boolean;
  metaDescription?: string | null;
  metaKey?: string | null;
}

// One row of the "Show tree" view, produced client-side by grouping on parentId.
export interface CategoryTreeRow {
  category: AdminCategoryInterface;
  depth: number;
  hasChildren: boolean;
  expanded: boolean;
}
