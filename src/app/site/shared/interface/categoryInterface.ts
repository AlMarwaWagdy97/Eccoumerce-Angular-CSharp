export interface CategoryInterface {
    id: number;
    parentId?: number | null,
    title: string,
    slug: string,
    description?: string | null,
    image?: string | null,
    sort?: number | null,
    metaDescription?: string | null,
    metaKey?: string | null,
    feature?: boolean,
    status?: boolean
}