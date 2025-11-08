import { CreateAddressDto } from "../address/CreateAddressDto";

export interface CreateClientDTO{
    
    id?: number;
    paymentIdentifier: number;
    firstName: string;
    lastName: string;
    registrationDate: Date;
    notes: string;
    dni: string;
    cuit?: string;
    preferredPaymentMethodId: number;
    ivaCondition: string;
    billingType?: string;
    billingTypeId?: number;
    startDate: Date;
    contractedM3: number;
    amount: number;
    lockerIds: number[];
    userID: number;

    emails: string[];
    phones: string[];
    addressDto: CreateAddressDto,

    prepaidMonths?: number;
    isLegacyClient?: boolean;

    // increaseFrequency: string;
    // increasePercentage: number;
}