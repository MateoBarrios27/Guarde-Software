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

    //contact info
    emails: string[];
    phones: string[];
    addressDto: CreateAddressDto,

    // Legacy client fields
    prepaidMonths?: number;
    isLegacyClient?: boolean;
    legacyInitialAmount?: number;
    legacyNextIncreaseDate?: Date;
    isLegacy6MonthPromo?: boolean;


    // increaseFrequency: string;
    // increasePercentage: number;
}