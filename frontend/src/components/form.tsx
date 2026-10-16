import { Controller, type Control, type FieldValues, type Path } from 'react-hook-form';
import { MenuItem, TextField, type TextFieldProps } from '@mui/material';

type FormTextProps<T extends FieldValues> = Omit<TextFieldProps, 'name'> & { control: Control<T>; name: Path<T> };

/** MUI TextField bound to react-hook-form, showing the field's validation message. */
export function FormText<T extends FieldValues>({ control, name, helperText, ...props }: FormTextProps<T>) {
  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => (
        <TextField
          {...props}
          name={field.name}
          value={field.value ?? ''}
          onChange={field.onChange}
          onBlur={field.onBlur}
          inputRef={field.ref}
          error={!!fieldState.error}
          helperText={fieldState.error?.message ?? helperText}
        />
      )}
    />
  );
}

export function FormSelect<T extends FieldValues>({
  options, ...props
}: FormTextProps<T> & { options: { value: string; label: string }[] }) {
  return (
    <FormText {...props} select>
      {options.map((o) => (
        <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>
      ))}
    </FormText>
  );
}

export const GENDER_OPTIONS = ['Male', 'Female', 'Other', 'Unknown'].map((g) => ({ value: g, label: g }));
