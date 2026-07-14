export interface FinancialInstitution {
  id: string;
  name: string;
  description?: string;
  imageUrl?: string;
  createdDate: string;
  updatedDate: string;
}

export interface FinancialInstitutionRequest {
  id?: string;
  name: string;
  description?: string;
  imageUrl?: string;
}
