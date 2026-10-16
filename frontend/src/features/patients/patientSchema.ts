import { z } from 'zod';
import type { PatientDto, SavePatientRequest } from '../../types/api';
import { emptyToNull } from '../../utils/format';

const phone = /^\+?[0-9 -]{10,20}$/;
const optionalPhone = z.string().refine((v) => !v.trim() || phone.test(v.trim()), 'Enter a valid phone number');

/** Mirrors SavePatientRequestValidator on the server. Form values are strings; converted on submit. */
export const patientSchema = z
  .object({
    firstName: z.string().trim().min(1, 'First name is required').max(80),
    lastName: z.string().trim().min(1, 'Last name is required').max(80),
    gender: z.enum(['Male', 'Female', 'Other', 'Unknown']),
    ageMode: z.enum(['dob', 'age']),
    dateOfBirth: z.string(),
    ageYears: z.string(),
    bloodGroup: z.string(),
    phone: z.string().trim().regex(phone, 'Enter a valid phone number (10-15 digits)'),
    alternatePhone: optionalPhone,
    email: z.string().refine((v) => !v.trim() || z.email().safeParse(v.trim()).success, 'Enter a valid email'),
    addressLine1: z.string().max(200),
    addressLine2: z.string().max(200),
    city: z.string().max(80),
    state: z.string().max(80),
    postalCode: z.string().max(12),
    country: z.string().max(60),
    emergencyContactName: z.string().max(150),
    emergencyContactPhone: optionalPhone,
    emergencyContactRelation: z.string().max(40),
  })
  .superRefine((v, ctx) => {
    if (v.ageMode === 'dob') {
      if (!v.dateOfBirth) ctx.addIssue({ code: 'custom', path: ['dateOfBirth'], message: 'Enter the date of birth' });
      else if (new Date(v.dateOfBirth) > new Date()) ctx.addIssue({ code: 'custom', path: ['dateOfBirth'], message: 'Date of birth cannot be in the future' });
    } else {
      const age = Number(v.ageYears);
      if (!v.ageYears.trim() || !Number.isInteger(age) || age < 0 || age > 130)
        ctx.addIssue({ code: 'custom', path: ['ageYears'], message: 'Enter an age between 0 and 130' });
    }
  });

export type PatientFormValues = z.infer<typeof patientSchema>;

export const emptyPatientForm: PatientFormValues = {
  firstName: '', lastName: '', gender: 'Male', ageMode: 'age', dateOfBirth: '', ageYears: '', bloodGroup: '',
  phone: '', alternatePhone: '', email: '', addressLine1: '', addressLine2: '', city: '', state: '', postalCode: '',
  country: 'India', emergencyContactName: '', emergencyContactPhone: '', emergencyContactRelation: '',
};

export function toPatientForm(p: PatientDto): PatientFormValues {
  return {
    firstName: p.firstName, lastName: p.lastName, gender: p.gender,
    ageMode: p.dateOfBirth ? 'dob' : 'age', dateOfBirth: p.dateOfBirth ?? '', ageYears: p.age?.toString() ?? '',
    bloodGroup: p.bloodGroup ?? '', phone: p.phone, alternatePhone: p.alternatePhone ?? '', email: p.email ?? '',
    addressLine1: p.addressLine1 ?? '', addressLine2: p.addressLine2 ?? '', city: p.city ?? '', state: p.state ?? '',
    postalCode: p.postalCode ?? '', country: p.country ?? '', emergencyContactName: p.emergencyContactName ?? '',
    emergencyContactPhone: p.emergencyContactPhone ?? '', emergencyContactRelation: p.emergencyContactRelation ?? '',
  };
}

export function toPatientRequest(v: PatientFormValues): SavePatientRequest {
  return {
    firstName: v.firstName.trim(), lastName: v.lastName.trim(), gender: v.gender,
    dateOfBirth: v.ageMode === 'dob' ? v.dateOfBirth : null,
    ageYears: v.ageMode === 'age' ? Number(v.ageYears) : null,
    bloodGroup: emptyToNull(v.bloodGroup), phone: v.phone.trim(), alternatePhone: emptyToNull(v.alternatePhone),
    email: emptyToNull(v.email), addressLine1: emptyToNull(v.addressLine1), addressLine2: emptyToNull(v.addressLine2),
    city: emptyToNull(v.city), state: emptyToNull(v.state), postalCode: emptyToNull(v.postalCode),
    country: emptyToNull(v.country), emergencyContactName: emptyToNull(v.emergencyContactName),
    emergencyContactPhone: emptyToNull(v.emergencyContactPhone), emergencyContactRelation: emptyToNull(v.emergencyContactRelation),
  };
}
