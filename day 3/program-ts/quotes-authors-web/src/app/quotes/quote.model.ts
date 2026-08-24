export interface Quote {
  id: number;
  author: string;
  text: string;
  publishedAt: string;
  isDeleted: boolean;
  createdByUserId: number;
}
