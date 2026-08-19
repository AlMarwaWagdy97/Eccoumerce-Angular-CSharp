export interface SliderInterface {
  id: number;
  title: string;
  image: string;
  link?: string | null;
  sort?: number | null;
  status: boolean;
  startsOn?: string | null;
  endsOn?: string | null;
}
