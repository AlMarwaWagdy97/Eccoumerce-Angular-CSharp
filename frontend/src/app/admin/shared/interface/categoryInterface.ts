export interface CategoryInterface {
    parentId?: number,
    title: string,
    slug: string,
    description?: string,
    image?: string,
    sort?: number,
    metaDescription?: string,
    metaKey?: string,
    feature?: boolean,
    status?: boolean
}
