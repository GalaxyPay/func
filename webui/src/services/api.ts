const AWN = {
  get api() {
    return axios.create({ baseURL: "http://localhost:3536" });
  },
};

// export async function getStatus(name: string) {
//   return AWN.api.get(`${name}`);
// }

// export async function createService(name: string) {
//   await AWN.api.post(`${name}`);
// }

// export async function startService(name: string) {
//   await AWN.api.put(`${name}/start`);
// }

// export async function stopService(name: string) {
//   await AWN.api.put(`${name}/stop`);
// }

// export async function deleteService(name: string) {
//   await AWN.api.delete(`${name}`);
// }

export default AWN;
