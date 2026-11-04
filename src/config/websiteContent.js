// Homepage content is separate from the parts catalog used by repair workflows.
export const websiteContentKinds = ['DEVICE', 'SERVICE', 'INSTALLATION', 'POLICY', 'CONTACT']
export const policyKinds = ['POLICY', 'WARRANTY', 'MAINTENANCE', 'RETURN']
export const websiteContentGroup = kind => policyKinds.includes(kind) ? 'POLICY' : kind
