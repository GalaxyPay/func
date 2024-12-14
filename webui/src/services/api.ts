const FUNC = {
  get api() {
    return axios.create({ baseURL: location.origin });
  },
};

export default FUNC;
